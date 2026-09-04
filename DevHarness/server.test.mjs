import assert from 'node:assert/strict'
import { once } from 'node:events'
import test from 'node:test'

import { createHarnessServer, normalizeServerUrl } from './server.mjs'

test('normalizeServerUrl accepts Jellyfin HTTP addresses without queries', () => {
  assert.equal(normalizeServerUrl('jellyfin.local:8096/'), 'http://jellyfin.local:8096')
  assert.equal(normalizeServerUrl('https://media.example.test/jellyfin/'), 'https://media.example.test/jellyfin')
})

test('normalizeServerUrl rejects unsafe or ambiguous inputs', () => {
  assert.throws(() => normalizeServerUrl('ftp://media.example.test'), /HTTP/)
  assert.throws(() => normalizeServerUrl('https://user:secret@media.example.test'), /凭据/)
  assert.throws(() => normalizeServerUrl('https://media.example.test/?token=value'), /查询参数/)
})

test('development API rejects requests outside the harness origin', async (context) => {
  const server = createHarnessServer()
  server.listen(0, '127.0.0.1')
  await once(server, 'listening')
  context.after(() => server.close())

  const address = server.address()
  assert.equal(typeof address, 'object')
  const baseUrl = `http://127.0.0.1:${address.port}`
  const health = await fetch(`${baseUrl}/health`)
  assert.equal(health.status, 200)

  const rejected = await fetch(`${baseUrl}/api/development-server`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: '{}',
  })
  assert.equal(rejected.status, 403)
  assert.equal((await rejected.json()).code, 'invalid_origin')
})

test('development API validates bounded JSON before any network request', async (context) => {
  const server = createHarnessServer()
  server.listen(0, '127.0.0.1')
  await once(server, 'listening')
  context.after(() => server.close())

  const address = server.address()
  assert.equal(typeof address, 'object')
  const response = await fetch(`http://127.0.0.1:${address.port}/api/login`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Origin: 'http://127.0.0.1:4177',
    },
    body: JSON.stringify({ serverUrl: 'file:///etc/passwd', username: 'tester', password: '' }),
  })
  assert.equal(response.status, 400)
  assert.equal((await response.json()).code, 'invalid_server')
})
