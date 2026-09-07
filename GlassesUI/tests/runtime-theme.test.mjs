import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'
import * as themes from '../../SharedUI/theme.mjs'
import * as subtitles from '../../SharedUI/subtitles.mjs'

const source = await readFile(new URL('../src/runtime.ts', import.meta.url), 'utf8')
const compiled = ts.transpileModule(source.replaceAll('import.meta.env.DEV', 'false'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText

test('theme bootstrap preserves catalog identity, deduplicates updates and uses the default for unknown values', async () => {
  const initial = {
    source: 'android', displayMode: 'mirror_2d', glassesConnected: true, catalogGeneration: 7,
    session: { serverUrl: 'https://media.example.test', serverName: 'Demo', serverVersion: '10.10',
      serverId: 'demo', accessToken: 'synthetic-token', userId: 'visitor', userName: 'Visitor', deviceId: 'test-device' },
  }
  const window = {
    RayNeoGlasses: { getBootstrapState: () => JSON.stringify(initial), ready() {} },
    localStorage: { getItem() { throw new Error('Native theme must not read browser storage') } },
  }
  const exports = {}
  vm.runInNewContext(compiled, {
    window, exports,
    require: (path) => {
      if (path === '../../SharedUI/subtitles.mjs') return subtitles
      assert.equal(path, '../../SharedUI/theme.mjs')
      return themes
    },
  })
  assert.equal((await exports.discoverRuntime()).uiTheme, 'liquid-glass')
  const received = []
  const unsubscribe = exports.subscribeRuntime(value => received.push(value))
  window.LucentNative.receiveBootstrapState({ ...initial, uiTheme: 'simpleUI' })
  assert.equal(received.at(-1).uiTheme, 'simpleUI')
  assert.equal(received.at(-1).catalogGeneration, 7)
  assert.equal(received.at(-1).displayMode, initial.displayMode)
  assert.equal(JSON.stringify(received.at(-1).session), JSON.stringify(initial.session))
  const count = received.length
  window.LucentNative.receiveBootstrapState(JSON.stringify({ ...initial, uiTheme: 'simpleUI' }))
  assert.equal(received.length, count)
  window.LucentNative.receiveBootstrapState({ ...initial, uiTheme: '<invalid-theme>' })
  assert.equal(received.at(-1).uiTheme, 'liquid-glass')
  unsubscribe()
  window.LucentNative.receiveBootstrapState({ ...initial, uiTheme: 'simpleUI' })
  assert.equal(received.length, count + 1)
})

test('glasses preferences wait for native acknowledgement, reject invalid edits and preserve catalog state', async () => {
  const initial = { source: 'android', displayMode: 'stereo_screen', catalogGeneration: 9, uiTheme: 'simpleUI', subtitleSize: 'normal', session: null }
  const sent = []
  const window = { RayNeoGlasses: { getBootstrapState: () => JSON.stringify(initial), ready() {}, postMessage: value => sent.push(JSON.parse(value)) } }
  const exports = {}
  vm.runInNewContext(compiled, { window, exports, require: path => path.endsWith('/theme.mjs') ? themes : subtitles })
  const runtime = await exports.discoverRuntime()
  const received = []
  exports.subscribeRuntime(value => received.push(value))
  assert.equal(exports.requestUiPreference({ type: 'set_subtitle_size', value: 'large' }, runtime), true)
  assert.equal(received.at(-1).subtitleSize, 'normal')
  assert.deepEqual(sent, [{ type: 'set_subtitle_size', value: 'large' }])
  for (const value of [null, {}, 1.5, 'LARGE', ' large', 'large ', 'x'.repeat(8193)]) {
    assert.equal(exports.requestUiPreference({ type: 'set_subtitle_size', value }, runtime), false)
  }
  assert.equal(sent.length, 1)
  window.LucentNative.receiveBootstrapState({ ...initial, subtitleSize: 'large' })
  assert.equal(received.at(-1).subtitleSize, 'large')
  assert.equal(received.at(-1).uiTheme, 'simpleUI')
  assert.equal(received.at(-1).catalogGeneration, 9)
  assert.equal(received.at(-1).displayMode, 'stereo_screen')
  const count = received.length
  window.LucentNative.receiveBootstrapState({ ...initial, subtitleSize: 'large' })
  assert.equal(received.length, count)
  window.LucentNative.receiveBootstrapState({ ...initial, subtitleSize: 'future-size' })
  assert.equal(received.at(-1).subtitleSize, 'normal')
})

test('standalone preview edits merge without changing runtime identity or persisting session data', () => {
  const stored = new Map()
  const exports = {}
  const window = {}
  vm.runInNewContext(compiled, { window, exports, require: path => path.endsWith('/theme.mjs')
    ? { ...themes, savePreviewTheme: value => stored.set(themes.PREVIEW_THEME_KEY, value) }
    : { ...subtitles, savePreviewSubtitleSize: value => stored.set(subtitles.PREVIEW_SUBTITLE_SIZE_KEY, value) } })
  const runtime = { source: 'development', displayMode: 'Mirror2D', catalogGeneration: 7, uiTheme: 'liquid-glass', subtitleSize: 'normal', session: { userId: 'synthetic' } }
  const received = []
  exports.subscribeRuntime(value => received.push(value))
  exports.requestUiPreference({ type: 'set_ui_theme', value: 'simpleUI' }, runtime)
  exports.requestUiPreference({ type: 'set_subtitle_size', value: 'extra-large' }, runtime)
  assert.equal(received.at(-1).uiTheme, 'simpleUI')
  assert.equal(received.at(-1).subtitleSize, 'extra-large')
  assert.equal(received.at(-1).source, 'development')
  assert.equal(received.at(-1).session, runtime.session)
  assert.equal(received.at(-1).catalogGeneration, 7)
  assert.deepEqual([...stored.values()], ['simpleUI', 'extra-large'])
})
