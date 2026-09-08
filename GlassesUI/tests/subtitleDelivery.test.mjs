import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'

const { code } = await transformWithEsbuild(
  await readFile(new URL('../src/jellyfin.ts', import.meta.url), 'utf8'),
  'jellyfin.ts', { target: 'es2022' },
)
const isolated = code.replace(
  /import \{ getNativeHardwareVideoCodecs \} from ["']\.\/runtime["'];?/,
  'const getNativeHardwareVideoCodecs = () => ["h264"]; const window = globalThis; const __APP_VERSION__ = "0.0.0-test";',
)
assert.notEqual(isolated, code)
const { JellyfinClient } = await import(`data:text/javascript;base64,${Buffer.from(isolated).toString('base64')}`)
const session = {
  serverUrl: 'https://media.example.invalid/jellyfin', accessToken: 'fixture-token',
  userId: 'user', deviceId: 'subtitle-test',
}

function playbackFixture(t, { codec = 'ass', external = false, container = 'mkv', delivery = '/original/Stream.ass' } = {}) {
  t.mock.method(globalThis, 'fetch', async (_url, init) => {
    requests.push(JSON.parse(init.body))
    return new Response(JSON.stringify({ PlaySessionId: 'play-session', MediaSources: [{
      Id: 'source', Container: container, Bitrate: 5_000_000, SupportsDirectPlay: true,
      TranscodingUrl: '/Videos/item/master.m3u8', DefaultSubtitleStreamIndex: 4,
      MediaStreams: [
        { Type: 'Video', Index: 0, Codec: 'h264', Width: 1920, Height: 1080, BitDepth: 8 },
        { Type: 'Audio', Index: 1, Codec: 'aac' },
        { Type: 'Subtitle', Index: 4, Codec: codec, IsExternal: external, DeliveryUrl: delivery },
        { Type: 'Subtitle', Index: 5, Codec: 'ssa', DeliveryUrl: '/original/Stream.ssa' },
      ],
    }] }), { headers: { 'Content-Type': 'application/json' } })
  })
  const requests = []
  const client = new JellyfinClient(session)
  return { requests, prepare: (selection = {}, ticks = 0) => client.preparePlayback({ id: 'item', canPlay: true }, ticks, selection) }
}

function expectWebVtt(plan, index) {
  const url = new URL(plan.subtitleUrl)
  assert.equal(url.pathname, `/jellyfin/Videos/item/source/Subtitles/${index}/Stream.vtt`)
  assert.equal(url.searchParams.get('api_key'), session.accessToken)
  assert.equal(url.searchParams.get('startPositionTicks'), '0')
  assert.equal(url.searchParams.get('copyTimestamps'), 'false')
  assert.equal(url.searchParams.get('addVttTimeMap'), 'false')
  assert.equal(plan.subtitleBurnedIn, false)
}

test('converts embedded ASS to WebVTT during HLS playback instead of fetching the original delivery', async (t) => {
  const { prepare, requests } = playbackFixture(t)
  const plan = await prepare()
  assert.equal(plan.playMethod, 'Transcode')
  expectWebVtt(plan, 4)
  for (const request of requests) {
    assert.deepEqual(request.DeviceProfile.SubtitleProfiles.filter(p => p.Method === 'External').map(p => p.Format), ['vtt', 'webvtt'])
    assert.equal(request.AlwaysBurnInSubtitleWhenTranscoding, false)
  }
})

test('converts external SSA while preserving direct video playback', async (t) => {
  const { prepare } = playbackFixture(t, { codec: 'ssa', external: true, container: 'mp4', delivery: 'https://media.example.invalid/raw.ssa' })
  const plan = await prepare()
  assert.equal(plan.playMethod, 'DirectPlay')
  expectWebVtt(plan, 4)
})

test('switching subtitle tracks at a resume position requests the selected track on the full media timeline', async (t) => {
  const { prepare } = playbackFixture(t)
  expectWebVtt(await prepare(), 4)
  const plan = await prepare({ subtitleStreamIndex: 5 }, 3_000_000_000)
  expectWebVtt(plan, 5)
  assert.equal(plan.startPositionTicks, 3_000_000_000)
  assert.equal(plan.subtitleStreamIndex, 5)
  const disabled = await prepare({ subtitleStreamIndex: -1 })
  assert.equal(disabled.subtitleUrl, undefined)
  assert.equal(disabled.subtitleStreamIndex, -1)
})

for (const codec of ['srt', 'subrip', 'vtt', 'webvtt', 'mov_text']) {
  test(`${codec} text subtitles use the same WebVTT delivery with or without a server URL`, async (t) => {
    const { prepare } = playbackFixture(t, { codec, delivery: codec === 'mov_text' ? null : `/original/Stream.${codec}` })
    expectWebVtt(await prepare(), 4)
  })
}

test('bitmap subtitles still request burn-in and have no local text URL', async (t) => {
  const { prepare, requests } = playbackFixture(t, { codec: 'pgssub', container: 'mp4' })
  const plan = await prepare()
  assert.equal(plan.playMethod, 'Transcode')
  assert.equal(plan.subtitleBurnedIn, true)
  assert.equal(plan.subtitleUrl, undefined)
  assert.equal(requests[1].AlwaysBurnInSubtitleWhenTranscoding, true)
  assert.ok(requests[1].DeviceProfile.SubtitleProfiles.some(p => p.Format === 'pgssub' && p.Method === 'Encode'))
})
