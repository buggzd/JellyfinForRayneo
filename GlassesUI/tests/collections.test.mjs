import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { transformWithEsbuild } from 'vite'

const moduleUrl = async (name, replace = value => value) => {
  const { code } = await transformWithEsbuild(await readFile(new URL(`../src/${name}.ts`, import.meta.url), 'utf8'), `${name}.ts`, { target: 'es2022' })
  return `data:text/javascript;base64,${Buffer.from(replace(code)).toString('base64')}`
}
const progressUrl = await moduleUrl('watchProgress')
const { JellyfinClient } = await import(await moduleUrl('jellyfin', code => code
  .replace(/from ["']\.\/watchProgress["']/, `from "${progressUrl}"`)
  .replace(/import \{ getNativeHardwareVideoCodecs \} from ["']\.\/runtime["'];?/, 'const getNativeHardwareVideoCodecs = () => ["h264"]; const window = globalThis; const __APP_VERSION__ = "0.0.0-test";')))


test('collection groups and BoxSets browse as containers while films and series open details', () => {
  const client = new JellyfinClient({ serverUrl: 'https://media.example.invalid', accessToken: 'fixture', userId: 'user', deviceId: 'test' })
  for (const dto of [
    { Id: 'group', Type: 'UserView', CollectionType: 'boxsets' },
    { Id: 'collection', Type: 'BoxSet', IsFolder: true },
    { Id: 'nested', Type: 'BoxSet', MediaType: 'Video' },
  ]) {
    const item = client.mapItem(dto)
    assert.equal(item.folder, true)
    assert.equal(item.canPlay, false)
  }
  assert.equal(client.mapItem({ Id: 'series', Type: 'Series', IsFolder: true }).folder, false)
  assert.equal(client.mapItem({ Id: 'movie', Type: 'Movie', MediaType: 'Video' }).canPlay, true)
})

test('loads collection groups, their collections and mixed contents one level at a time', async (t) => {
  const requests = []
  const children = {
    group: [{ Id: 'collection', Type: 'BoxSet' }],
    collection: [{ Id: 'movie', Type: 'Movie' }, { Id: 'series', Type: 'Series' }, { Id: 'nested', Type: 'BoxSet' }],
    nested: [{ Id: 'another-movie', Type: 'Movie' }],
  }
  t.mock.method(globalThis, 'fetch', async value => {
    const url = new URL(value)
    requests.push(url.searchParams)
    return Response.json({ Items: children[url.searchParams.get('ParentId')] })
  })
  const client = new JellyfinClient({ serverUrl: 'https://media.example.invalid', accessToken: 'fixture', userId: 'user', deviceId: 'test' })
  const collections = await client.loadFolder(client.mapItem({ Id: 'group', Type: 'UserView', CollectionType: 'boxsets' }))
  assert.deepEqual(collections.map(item => item.id), ['collection'])
  assert.equal(requests[0].get('IncludeItemTypes'), 'BoxSet')
  const contents = await client.loadFolder(collections[0])
  assert.deepEqual(contents.map(item => item.id), ['movie', 'series', 'nested'])
  assert.equal(requests[1].has('IncludeItemTypes'), false)
  assert.deepEqual((await client.loadFolder(contents[2])).map(item => item.id), ['another-movie'])
  assert.ok(requests.every(query => query.get('Recursive') === 'false'))
})
