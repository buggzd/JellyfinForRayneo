import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'
import * as themes from '../../SharedUI/theme.mjs'

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
