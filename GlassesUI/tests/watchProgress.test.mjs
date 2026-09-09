import { resolveI18nImport } from './i18n-test-helper.mjs'
import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'

const moduleUrl = async (name, replace = value => value) => {
  const { code } = await transformWithEsbuild(await readFile(new URL(`../src/${name}.ts`, import.meta.url), 'utf8'), `${name}.ts`, { target: 'es2022' })
  return `data:text/javascript;base64,${Buffer.from(resolveI18nImport(replace(code))).toString('base64')}`
}
const progressUrl = await moduleUrl('watchProgress')
const { WatchProgress, latestWatchedEpisode, resumeProgress, watchedTime } = await import(progressUrl)
const { JellyfinClient } = await import(await moduleUrl('jellyfin', code => code
  .replace(/from ["']\.\/watchProgress["']/, `from "${progressUrl}"`)
  .replace(/import \{ getNativeHardwareVideoCodecs \} from ["']\.\/runtime["'];?/, 'const getNativeHardwareVideoCodecs = () => ["h264"]; const window = globalThis; const __APP_VERSION__ = "0.0.0-test";')))
const ticks = seconds => seconds * 10_000_000
const episode = (id, extra = {}) => ({ id, title: '测试剧集', subtitle: 'S02 E08 · 测试单集', kind: '剧集', art: 0,
  sourceType: 'Episode', seriesId: 'series', seasonId: 'season2', canPlay: true, runtimeTicks: ticks(1500), ...extra })

test('selects the latest watched episode instead of the first unfinished episode and formats exact seconds', () => {
  const first = episode('early', { playbackPositionTicks: ticks(900), lastPlayedDate: '2026-09-01T00:00:00Z' })
  const recent = episode('recent', { playbackPositionTicks: ticks(125.9), lastPlayedDate: '2026-09-08T00:00:00Z' })
  assert.equal(latestWatchedEpisode([first, recent]).id, 'recent')
  assert.equal(watchedTime(recent), '2 分 05 秒')
  assert.equal(watchedTime(episode('long', { playbackPositionTicks: ticks(3705) })), '61 分 45 秒')
  assert.equal(resumeProgress(recent), 125.9 / 1500 * 100)
  assert.equal(resumeProgress(episode('bad', { playbackPositionTicks: Infinity })), 0)
  assert.equal(latestWatchedEpisode([episode('new')]), undefined)
})

test('preserves short sessions, clears a completed resume, and allows newer server or explicit watched edits', () => {
  const history = new WatchProgress()
  const item = episode('recent')
  history.record(item, ticks(2), ticks(1500))
  assert.equal(history.patch(item).playbackPositionTicks, ticks(2))
  assert.equal(history.patch({ ...item, lastPlayedDate: '2099-01-01T00:00:00Z' }).playbackPositionTicks, undefined)
  history.record(item, ticks(1500), ticks(1500))
  assert.equal(history.patch(item).watched, true)
  assert.equal(history.patch(item).playbackPositionTicks, 0)
  history.forget(item.id)
  assert.equal(history.patch(item), item)
  assert.equal(new WatchProgress().latestFor({ id: 'series' }), undefined)
})

test('detail waits for stop reporting, selects the last played season and overlays exact short-play progress', async (t) => {
  let finishStop
  let stopSent = false
  const requests = []
  const dto = { Id: 'recent', Name: '测试单集', Type: 'Episode', SeriesId: 'series', SeasonId: 'season2', RunTimeTicks: ticks(1500), IndexNumber: 8, UserData: {} }
  t.mock.method(globalThis, 'fetch', async (value, init) => {
    const url = new URL(value)
    requests.push(url)
    if (url.pathname.endsWith('/Sessions/Playing/Stopped')) {
      stopSent = true
      await new Promise(resolve => { finishStop = resolve })
      // Jellyfin can clear positions below its minimum resume duration.
      dto.UserData = { LastPlayedDate: new Date().toISOString(), PlaybackPositionTicks: 0 }
      return new Response(null, { status: 204 })
    }
    if (url.pathname.endsWith('/PlaybackInfo')) return Response.json({ PlaySessionId: 'visit', MediaSources: [{
      Id: 'source', Container: 'mp4', Bitrate: 5_000_000, SupportsDirectPlay: true, RunTimeTicks: ticks(1500), MediaStreams: [{ Type: 'Video', Codec: 'h264', Width: 1920, Height: 1080, BitDepth: 8 }, { Type: 'Audio', Codec: 'aac' }],
    }] })
    if (url.pathname.endsWith('/Sessions/Playing')) return new Response(null, { status: 204 })
    if (url.pathname.endsWith('/Items/series')) return Response.json({ Id: 'series', Type: 'Series', Name: '测试剧集' })
    if (url.pathname.endsWith('/Seasons')) return Response.json({ Items: [{ Id: 'season1', Type: 'Season' }, { Id: 'season2', Type: 'Season' }] })
    if (url.pathname.endsWith('/Episodes')) return Response.json({ Items: [dto] })
    if (url.pathname.endsWith('/Items')) return Response.json({ Items: [dto] })
    return Response.json([])
  })
  const client = new JellyfinClient({ serverUrl: 'https://media.example.invalid', accessToken: 'fixture-token', userId: 'user', deviceId: 'test' })
  const plan = await client.preparePlayback(episode('recent'), 0)
  await client.reportPlaybackStarted(plan, false, 0)
  const stopping = client.reportPlaybackStopped(plan, ticks(125))
  assert.equal(stopSent, true)
  const detail = client.loadDetail('series')
  await Promise.resolve()
  assert.equal(requests.some(url => url.pathname.endsWith('/Items/series')), false)
  finishStop()
  await stopping
  const result = await detail
  assert.equal(result.selectedSeasonId, 'season2')
  assert.equal(result.episodes[0].playbackPositionTicks, ticks(125))
  assert.equal((await client.loadDetail('series', 'season1')).selectedSeasonId, 'season1')
  const otherAccount = new JellyfinClient({ serverUrl: 'https://media.example.invalid', accessToken: 'other-fixture', userId: 'other', deviceId: 'test' })
  assert.equal((await otherAccount.loadDetail('series')).episodes[0].playbackPositionTicks, 0)
})

test('failed stop requests release the refresh barrier and retain the local position', async () => {
  const history = new WatchProgress()
  history.record(episode('recent'), ticks(125), ticks(1500))
  let reject
  const stop = history.track(new Promise((_resolve, fail) => { reject = fail }))
  const caught = assert.rejects(stop)
  const refresh = history.settle()
  reject(new Error('offline'))
  await Promise.all([caught, refresh])
  assert.equal(history.patch(episode('recent')).playbackPositionTicks, ticks(125))
})


test('late confirmation of an earlier episode cannot move it ahead of a newer watch', (t) => {
  const history = new WatchProgress()
  t.mock.method(Date, 'now', () => Date.parse('2099-01-01T00:00:00Z'))
  const earlier = history.record(episode('earlier'), ticks(125), ticks(1500))
  const later = history.record(episode('later'), ticks(135), ticks(1500))
  history.confirm(earlier)
  assert.equal(history.latestFor({ id: 'series' }).id, 'later')
  assert.equal(history.patch({ ...episode('earlier'), lastPlayedDate: '2098-01-01T00:00:00Z' }).playbackPositionTicks, ticks(125))
  assert.equal(latestWatchedEpisode([history.patch(episode('earlier')), later]).id, 'later')
})


test('rewatching a completed episode preserves its watched badge and precise resume position', () => {
  const history = new WatchProgress()
  const item = episode('rewatch', { watched: true })
  history.record(item, ticks(125), ticks(1500))
  assert.equal(history.patch(item).watched, true)
  assert.equal(history.patch(item).playbackPositionTicks, ticks(125))
  history.forget(item.id)
  assert.equal(history.patch({ ...item, watched: false }).watched, false)
})

test('server completion at its watched threshold takes precedence over a partial local record', () => {
  const history = new WatchProgress()
  const item = episode('threshold')
  history.record(item, ticks(1450), ticks(1500))
  assert.equal(history.patch({ ...item, watched: true }).watched, true)
  assert.equal(history.patch(item).watched, false)
})

test('switching language reformats generated metadata while retaining server text and watch progress', async () => {
  const { applyLanguage } = await import('../../SharedUI/i18n.mjs')
  const client = new JellyfinClient({ serverUrl: 'https://media.example.invalid', accessToken: 'fixture', userId: 'user', deviceId: 'test' })
  const item = client.mapItem({ Id: 'localized', Type: 'Movie', Name: '设置', Overview: 'An original synopsis.', RunTimeTicks: 36000000000 })
  const progress = new WatchProgress()
  progress.record(item, 100000000, item.runtimeTicks)
  const patched = progress.patch(item)
  try {
    applyLanguage('en')
    assert.equal(patched.title, '设置')
    assert.equal(patched.overview, 'An original synopsis.')
    assert.equal(patched.duration, '1 h')
    applyLanguage('zh-CN')
    assert.equal(patched.duration, '1 小时')
    assert.equal(patched.playbackPositionTicks, 100000000)
  } finally { applyLanguage('zh-CN') }
})
