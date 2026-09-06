import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'

const { code } = await transformWithEsbuild(
  await readFile(new URL('../src/playbackInfo.ts', import.meta.url), 'utf8'),
  'playbackInfo.ts', { target: 'es2022' },
)
const { playbackInfoRows, samplePlaybackStats, playbackMethodLabel } =
  await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)

const sourcePlan = {
  url: 'https://media.example.invalid/video', playMethod: 'DirectPlay', transcoding: false,
  container: 'MP4', videoCodec: 'HEVC', audioCodec: 'DTS', width: 3840, height: 2160,
  subtitleStreamIndex: -1, subtitleBurnedIn: false, subtitleTracks: [],
  mediaInfo: { size: 2 * 1024 ** 3, bitrate: 15_000_000, videoBitrate: 14_000_000,
    frameRate: 23.976, profile: 'Main 10', bitDepth: 10, pixelFormat: 'yuv420p10le',
    videoRange: 'HDR10', colorSpace: 'bt2020nc', audioChannels: 6, audioSampleRate: 48000, audioBitrate: 768000 },
}
const value = (rows, label) => rows.find((entry) => entry.label === label)?.value
const ranges = (...values) => ({ length: values.length, start: (index) => values[index][0], end: (index) => values[index][1] })
const probe = (overrides = {}) => ({
  readyState: 4, videoWidth: 1920, videoHeight: 1080, currentTime: 12, buffered: ranges([0, 20], [30, 60]),
  getVideoPlaybackQuality: () => ({ totalVideoFrames: 100, droppedVideoFrames: 3 }), ...overrides,
})

test('separates the playing HLS rendition from the original video and selected audio', () => {
  const plan = { ...sourcePlan, transcoding: true, playMethod: 'Transcode' }
  const rows = playbackInfoRows(plan, { videoCodec: 'avc1.640028', audioCodec: 'mp4a.40.2', width: 1920,
    height: 1080, bitrate: 5_000_000, frameRate: 24 }, ['h264'])
  assert.equal(value(rows.current, '播放视频'), 'H.264 · 1920 × 1080')
  assert.equal(value(rows.current, '帧率 / 码率'), '24 fps · 5 Mbps')
  assert.equal(value(rows.current, '音频 / 字幕'), 'AAC · 关闭')
  assert.equal(value(rows.original, '源视频'), 'HEVC · 3840 × 2160')
  assert.equal(value(rows.original, '帧率 / 码率'), '23.976 fps · 14 Mbps')
  assert.equal(value(rows.original, '源音频'), 'DTS · 6 声道 · 48 kHz · 768 kbps')
  assert.equal(value(rows.original, '封装 / 大小'), 'MP4 · 2 GiB')
})

test('does not infer actual hardware decoding from capability or direct play', () => {
  const rows = playbackInfoRows(sourcePlan, {}, ['hevc'])
  assert.equal(value(rows.current, '解码方式'), '系统自动（WebView）')
  assert.equal(value(rows.current, '硬解能力'), '支持 HEVC')
  assert.equal(value(playbackInfoRows(sourcePlan, {}, []).current, '硬解能力'), '未报告 HEVC 支持')
  assert.equal(value(playbackInfoRows(sourcePlan, {}, null).current, '硬解能力'), '未提供能力信息')
})

test('a fallback waits for output metadata instead of reusing the source format or requested codec', () => {
  const plan = { ...sourcePlan, transcoding: true, playMethod: 'Transcode',
    url: 'https://media.example.invalid/stream.m3u8?VideoCodec=h264&AudioCodec=aac&VideoBitrate=5000000' }
  const rows = playbackInfoRows(plan, {}, ['hevc', 'h264'])
  assert.equal(value(rows.current, '播放视频'), '等待媒体数据')
  assert.equal(value(rows.current, '帧率 / 码率'), '帧率未提供 · 码率未提供')
  assert.equal(value(rows.current, '硬解能力'), '等待播放格式')
  assert.equal(value(rows.current, '请求编码'), 'H.264 · AAC')
  assert.equal(playbackMethodLabel(plan), '服务器转码')
})

test('buffering counts only contiguous playable seconds and preserves zero counters', () => {
  assert.equal(samplePlaybackStats(probe(), null).bufferSeconds, 8)
  assert.equal(samplePlaybackStats(probe({ currentTime: 25 }), null).bufferSeconds, 0)
  assert.equal(samplePlaybackStats(probe({ currentTime: 40 }), null).bufferSeconds, 20)
  const stats = samplePlaybackStats(probe({ getVideoPlaybackQuality: () => ({ totalVideoFrames: 0, droppedVideoFrames: 0 }) }), null)
  assert.equal(value(playbackInfoRows(sourcePlan, stats, null).current, '丢帧 / 总帧'), '0 / 0 帧')
  assert.deepEqual(samplePlaybackStats(probe({ readyState: 0 }), null), {})
})

test('uses the active HLS level and handles older WebViews without quality counters', () => {
  const hls = { currentLevel: 1, audioTrack: 0, levels: [{ videoCodec: 'hvc1', bitrate: 9000000 },
    { videoCodec: 'avc1.640028', audioCodec: 'mp4a.40.2', frameRate: 25, averageBitrate: 0, bitrate: 3000000 }],
    audioTracks: [{ audioCodec: 'ec-3' }] }
  const stats = samplePlaybackStats(probe({ getVideoPlaybackQuality: undefined }), hls)
  assert.equal(stats.videoCodec, 'avc1.640028')
  assert.equal(stats.audioCodec, 'ec-3')
  assert.equal(stats.bitrate, 3000000)
  assert.equal(stats.frameRate, 25)
  assert.equal(stats.totalFrames, undefined)
  assert.doesNotThrow(() => samplePlaybackStats(probe({ buffered: { length: 1, start() { throw new Error('range changed') } } }), null))
})

test('shows subtitle delivery and ignores unsafe or unavailable technical fields', () => {
  const plan = { ...sourcePlan, transcoding: true, playMethod: 'Transcode', subtitleStreamIndex: 3,
    subtitleBurnedIn: true, subtitleTracks: [{ index: 3, codec: 'PGSSUB' }],
    url: 'https://media.example.invalid/stream?api_key=fixture-secret&VideoCodec=https://private.example/path&TranscodeReasons=SubtitleCodecNotSupported,arbitrary-server-text,__proto__,toString',
    container: 'https://private.example/secret', mediaInfo: { frameRate: NaN, bitrate: Infinity, bitDepth: -1, profile: '/private/file' } }
  const rows = playbackInfoRows(plan, {}, null)
  assert.match(value(rows.current, '音频 / 字幕'), /PGSSUB · 烧录到视频/)
  assert.equal(value(rows.current, '转码原因'), '字幕需要烧录')
  assert.equal(value(rows.current, '请求编码'), undefined)
  assert.doesNotMatch(JSON.stringify(rows), /private|secret|arbitrary-server|NaN|Infinity|-1 bit/)
  assert.equal(playbackMethodLabel(null), '正在准备')
})
