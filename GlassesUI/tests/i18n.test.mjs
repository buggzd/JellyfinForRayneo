import assert from 'node:assert/strict'
import { readFile, readdir } from 'node:fs/promises'
import test from 'node:test'
import ts from 'typescript'
import { applyLanguage, getLocale, getLanguage, normalizeLanguage, resolveLanguage, t, nativeMessage, subscribeLanguage } from '../../SharedUI/i18n.mjs'
const messages = JSON.parse(await readFile(new URL('../../SharedUI/locales/en.json', import.meta.url)))

test('follows system language with Chinese regional variants and an English fallback', () => {
  for (const system of ['zh-CN', 'zh-TW', 'zh-Hant-HK', 'ZH']) assert.equal(resolveLanguage('system', system), 'zh-CN')
  for (const system of ['en-GB', 'fr-FR', 'ja', '']) assert.equal(resolveLanguage('system', system), 'en')
  assert.equal(resolveLanguage('en', 'zh-CN'), 'en')
  assert.equal(resolveLanguage('zh-CN', 'en'), 'zh-CN')
  for (const value of [null, {}, 1, 'EN', 'en ', ' zh-CN']) assert.equal(normalizeLanguage(value), 'system')
})

test('live changes notify only on changes and preserve literal interpolation values', () => {
  applyLanguage('zh-CN')
  let changes = 0
  const unsubscribe = subscribeLanguage(() => changes++)
  applyLanguage('en')
  assert.equal(t('当前用户 {0}', { 0: '<img>{1}$&' }), 'Current user: <img>{1}$&')
  assert.equal(t('未知的新词条'), '未知的新词条')
  assert.equal(t('toString'), 'toString')
  applyLanguage('en')
  assert.equal(changes, 1)
  applyLanguage('system', 'zh-TW')
  assert.equal(getLanguage(), 'system')
  assert.equal(getLocale(), 'zh-CN')
  assert.equal(t('当前用户 {0}', { 0: 'Alex' }), '当前用户 Alex')
  assert.equal(changes, 2)
  unsubscribe()
})

test('fixed native diagnostics translate without reinterpreting arbitrary text', () => {
  applyLanguage('en')
  assert.equal(nativeMessage('发现 2 台 Jellyfin 服务器。'), 'Found 2 Jellyfin servers.')
  assert.equal(nativeMessage('Jellyfin 请求失败（HTTP 503），请检查服务器。'), 'Jellyfin request failed (HTTP 503). Check your server.')
  const unknown = 'A user title containing 设置 and {0}'
  assert.equal(nativeMessage(unknown), unknown)
})

test('English catalog covers every literal translation call with matching placeholders', async () => {
  for (const [key, value] of Object.entries(messages)) {
    assert.ok(value.trim(), key)
    const placeholders = value => [...value.matchAll(/\{\w+\}/g)].map(match => match[0]).sort()
    assert.deepEqual(placeholders(value), placeholders(key), key)
  }
  for (const surface of ['GlassesUI', 'CompanionUI']) {
    const directory = new URL(`../../${surface}/src/`, import.meta.url)
    for (const name of await readdir(directory)) {
      if (!/\.[jt]sx?$/.test(name)) continue
      const source = ts.createSourceFile(name, await readFile(new URL(name, directory), 'utf8'), ts.ScriptTarget.Latest, true)
      const visit = node => {
        if (ts.isCallExpression(node) && node.expression.getText(source) === 't' && ts.isStringLiteral(node.arguments[0])) {
          assert.ok(Object.hasOwn(messages, node.arguments[0].text), `${surface}/${name}: ${node.arguments[0].text}`)
        }
        ts.forEachChild(node, visit)
      }
      visit(source)
    }
  }
})
