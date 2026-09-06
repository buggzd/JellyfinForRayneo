import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { randomUUID } from 'node:crypto'
import vm from 'node:vm'
import test from 'node:test'

const source = await readFile(new URL('./harness.js', import.meta.url), 'utf8')
const settle = () => new Promise(resolve => setImmediate(resolve))

async function harness() {
  const messages = []
  const elements = new Map()
  const element = selector => {
    if (!elements.has(selector)) elements.set(selector, {
      value: '393x852', clientWidth: 600, clientHeight: 900, lastChild: {},
      style: { setProperty() {} }, addEventListener() {},
      contentWindow: { postMessage(message) { messages.push(JSON.parse(JSON.stringify(message))) } },
    })
    return elements.get(selector)
  }
  const context = vm.createContext({
    URL, Blob, crypto: { randomUUID },
    document: { querySelector: element },
    window: {
      location: { hostname: '127.0.0.1', origin: 'http://127.0.0.1:4177' },
      localStorage: { getItem() { return null }, setItem() {} },
      addEventListener() {}, setTimeout() {}, clearTimeout() {},
    },
    ResizeObserver: class { observe() {} },
    fetch: async () => ({ ok: false, json: async () => ({ error: '测试登录失败。', code: 'missing_config' }) }),
  })
  vm.runInContext(source, context)
  await settle()
  const call = (name, payload) => {
    context.input = payload
    return vm.runInContext(`${name}(input)`, context)
  }
  return {
    call,
    command: (method, ...args) => call('handleCompanionCall', { method, args }),
    state: () => messages.filter(message => message.target === 'companion' && message.type === 'state').at(-1).payload,
    generation: () => messages.filter(message => message.type === 'bootstrap').at(-1).payload.catalogGeneration,
  }
}

const account = (serverUrl = 'https://home.example.test', userId = 'first-user') => ({
  serverUrl, serverName: 'Demo library', serverVersion: '10.10', serverId: serverUrl,
  accessToken: 'test-token-do-not-publish-to-phone', userId, userName: userId, deviceId: 'demo-device',
})

test('switches between two servers and multiple users with only metadata on the phone', async () => {
  const app = await harness()
  app.call('applySession', account())
  const first = app.state().activeSessionId
  app.call('applySession', account(undefined, 'second-user'))
  app.call('applySession', account('https://cinema.example.test', 'first-user'))
  assert.equal(app.state().accounts.length, 3)
  app.command('activateSession', first)
  assert.equal(app.state().activeSessionId, first)
  assert.equal(app.state().serverUrl, 'https://home.example.test')
  assert.equal(app.state().username, 'first-user')
  assert.equal(JSON.stringify(app.state()).includes('test-token-do-not-publish-to-phone'), false)
  assert.equal(JSON.stringify(app.state()).includes('accessToken'), false)
  assert.equal(app.state().accounts.filter(entry => entry.active).length, 1)
})

test('browsing servers, failed login and cancelling login preserve the active connection', async () => {
  const app = await harness()
  app.call('applySession', account())
  const first = app.state().activeSessionId
  app.command('selectServer', 'https://another.example.test', 'Another library')
  assert.equal(app.state().serverUrl, 'https://home.example.test')
  assert.equal(app.state().loginServerUrl, 'https://another.example.test')
  app.command('login', 'https://another.example.test', 'new-user', 'test-password', true)
  await settle()
  assert.equal(app.state().activeSessionId, first)
  assert.equal(app.state().sessionAvailable, true)
  assert.equal(app.state().isError, true)
  app.command('cancelQuickConnect')
  assert.equal(app.state().activeSessionId, first)
  assert.equal(app.state().accounts.length, 1)
})

test('removing an inactive account leaves playback and the current account intact', async () => {
  const app = await harness()
  app.call('applySession', account())
  const first = app.state().activeSessionId
  app.call('applySession', account('https://cinema.example.test'))
  const second = app.state().activeSessionId
  app.call('handleGlassesMessage', { type: 'playback_state', state: 'playing', title: 'Demo film' })
  app.command('removeSession', first)
  assert.equal(app.state().activeSessionId, second)
  assert.equal(app.state().playback.state, 'playing')
  assert.equal(app.state().accounts.length, 1)
  app.command('activateSession', first)
  assert.equal(app.state().activeSessionId, second)
})

test('late unauthorized response from the previous account cannot remove a newly activated account', async () => {
  const app = await harness()
  app.call('applySession', account())
  const first = app.state().activeSessionId
  const previousGeneration = app.generation()
  app.call('applySession', account('https://cinema.example.test'))
  const second = app.state().activeSessionId
  app.call('handleGlassesMessage', { type: 'unauthorized', catalogGeneration: previousGeneration })
  assert.equal(app.state().activeSessionId, second)
  assert.equal(app.state().accounts.length, 2)
  app.call('handleGlassesMessage', { type: 'unauthorized', catalogGeneration: app.generation() })
  assert.equal(app.state().sessionAvailable, false)
  assert.equal(app.state().accounts.length, 1)
  app.command('activateSession', first)
  assert.equal(app.state().sessionAvailable, true)
  assert.equal(app.state().activeSessionId, first)
})

test('successful account switch clears the previous playback and search state', async () => {
  const app = await harness()
  app.call('applySession', account())
  const first = app.state().activeSessionId
  app.call('applySession', account('https://cinema.example.test'))
  app.call('handleGlassesMessage', { type: 'playback_state', state: 'playing', title: 'Demo film' })
  app.call('handleGlassesMessage', { type: 'search_state', state: 'active', query: 'demo' })
  app.command('activateSession', first)
  assert.equal(app.state().playback.state, 'stopped')
  assert.equal(app.state().searchInputActive, false)
  assert.equal(app.state().searchQuery, '')
})
