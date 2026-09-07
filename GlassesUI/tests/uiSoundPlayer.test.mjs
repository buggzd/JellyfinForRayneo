import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'

const { code } = await transformWithEsbuild(await readFile(new URL('../src/uiSoundPlayer.ts', import.meta.url), 'utf8'),
  'uiSoundPlayer.ts', { target: 'es2022' })
const { UiSoundPlayer, uiSoundStorageKey } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`)

function fixture(saved) {
  const storage = new Map(saved === undefined ? [] : [[uiSoundStorageKey, saved]])
  const clips = new Map()
  const calls = []
  let now = 0
  let visible = true
  const player = new UiSoundPlayer(sound => {
    const clip = { currentTime: 0, pause() { calls.push(['pause', sound]) }, play() { calls.push(['play', sound]); return Promise.resolve() } }
    clips.set(sound, clip)
    return clip
  }, () => ({ getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value) }),
  () => visible, () => now)
  return { player, clips, calls, storage, advance: ms => { now += ms }, hide: () => { visible = false } }
}

test('mute survives recreation, stops the active clip and prevents all future sounds', () => {
  const f = fixture()
  assert.equal(f.player.isEnabled(), true)
  f.player.play('open')
  f.player.setEnabled(false)
  f.player.play('focus')
  f.player.play('error')
  assert.deepEqual(f.calls, [['play', 'open'], ['pause', 'open']])
  assert.equal(f.storage.get(uiSoundStorageKey), 'off')
  assert.equal(fixture(f.storage.get(uiSoundStorageKey)).player.isEnabled(), false)
  f.player.setEnabled(true)
  f.player.play('toggle-on')
  assert.deepEqual(f.calls.at(-1), ['play', 'toggle-on'])
})

test('rapid directions are throttled and a distinct action interrupts the old clip', () => {
  const f = fixture()
  f.player.play('focus')
  f.advance(20)
  f.player.play('focus')
  assert.equal(f.calls.length, 1)
  f.player.play('select')
  assert.deepEqual(f.calls, [['play', 'focus'], ['pause', 'focus'], ['play', 'select']])
  f.advance(300)
  f.player.play('focus')
  assert.equal(f.clips.size, 2, 'clips are reused, not allocated for each gesture')
  assert.equal(f.clips.get('focus').currentTime, 0)
})

test('a continuous boundary input sounds once until focus or direction changes', () => {
  const f = fixture()
  const topItem = {}
  f.player.playBoundaryOnce(topItem, 'up')
  f.advance(1_000)
  f.player.playBoundaryOnce(topItem, 'up')
  f.advance(1_000)
  f.player.playBoundaryOnce(topItem, 'up')
  assert.deepEqual(f.calls, [['play', 'boundary']])

  f.player.playBoundaryOnce(topItem, 'left')
  assert.deepEqual(f.calls.slice(-2), [['pause', 'boundary'], ['play', 'boundary']])
  f.player.resetBoundary()
  f.advance(300)
  f.player.playBoundaryOnce(topItem, 'up')
  assert.equal(f.calls.filter(([action]) => action === 'play').length, 3)
})

test('stale rejected play cannot lose the active clip after reuse, and mute aborts it', async () => {
  const f = fixture()
  f.player.preload()
  let rejectOld
  const clip = f.clips.get('focus')
  clip.play = () => new Promise((resolve, reject) => { rejectOld = reject })
  f.player.play('focus')
  clip.play = () => Promise.resolve()
  f.advance(100)
  f.player.play('focus')
  rejectOld(new Error('old play aborted'))
  await Promise.resolve()
  f.player.setEnabled(false)
  assert.deepEqual(f.calls, [['pause', 'focus'], ['pause', 'focus']])
})

test('hidden pages stay silent and unavailable audio/storage never blocks interaction', () => {
  const f = fixture('unexpected')
  assert.equal(f.player.isEnabled(), true)
  f.hide()
  f.player.play('notification')
  assert.equal(f.clips.size, 0)
  const player = new UiSoundPlayer(() => { throw new Error('audio unavailable') },
    () => { throw new Error('storage denied') }, () => true)
  assert.doesNotThrow(() => player.preload())
  assert.doesNotThrow(() => player.play('select'))
  assert.equal(player.setEnabled(false), false)
  assert.equal(player.isEnabled(), false)
})
