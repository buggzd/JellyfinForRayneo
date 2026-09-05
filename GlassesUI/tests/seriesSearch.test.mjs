import assert from 'node:assert/strict'
import { test } from 'node:test'
import { loadSeriesSearch } from '../scripts/loadSeriesSearch.mjs'

const { buildSeriesIndex, parseSeriesQuery, searchSeries } = await loadSeriesSearch()

const series = (id, title, extra = {}) => ({
  id, title, subtitle: '', kind: '剧集', art: 0, sourceType: 'Series', ...extra,
})

test('finds the same series by title, pinyin, initials, English and alias', async () => {
  const entries = await buildSeriesIndex([
    series('sea', '深海回声', { original: 'Echoes of the Abyss', aliases: ['海底记忆'] }),
    series('garden', '以太花园', { original: 'Aether Garden' }),
  ])
  for (const [query, reason] of [
    ['深海回声', '标题'],
    ['shenhaihuisheng', '拼音'],
    ['shhs', '首字母'],
    ['ＥＣＨＯＥＳ of the Abyss', '标题'],
    ['海底记忆', '标题'],
    ['hdjy', '首字母'],
    ['snhi', '模糊匹配'],
  ]) {
    const [match] = searchSeries(entries, query)
    assert.equal(match?.item.id, 'sea', query)
    assert.equal(match?.reason, reason, query)
  }
  assert.deepEqual(searchSeries(entries, 'zzzzzzzz'), [])
})

test('retains episode hints and query normalization', async () => {
  const entries = await buildSeriesIndex([series('sea', '深海回声')])
  assert.deepEqual(parseSeriesQuery('shhs s02e003'), { term: 'shhs', seasonHint: 2, episodeHint: 3 })
  assert.deepEqual(parseSeriesQuery('shhs ep12'), { term: 'shhs', episodeHint: 12 })
  assert.deepEqual(searchSeries(entries, 'shhs s02e003'), searchSeries(entries, 'shhs'))
  assert.deepEqual(searchSeries(entries, ' ＳＨＨＳ '), searchSeries(entries, 'shhs'))
})

test('keeps recommendation boosts, dates, Chinese collation and stable ties', async () => {
  const entries = await buildSeriesIndex([
    series('old', '白云', { dateCreated: '2024-01-01' }),
    series('garden', '花园', { dateCreated: '2025-01-01' }),
    series('sea', '海洋', { dateCreated: '2025-01-01' }),
    series('sea-copy', '海洋', { dateCreated: '2025-01-01' }),
    series('favorite', '星空', { favorite: true }),
    series('resume', '山川', { progress: 15 }),
  ])
  assert.deepEqual(searchSeries(entries, '').map(({ item }) => item.id), [
    'favorite', 'resume', 'sea', 'sea-copy', 'garden', 'old',
  ])
  assert.equal(searchSeries(entries, '', 12, ['old'])[0].item.id, 'old')
  assert.equal(searchSeries(entries, '花园', 12, ['old'])[0].item.id, 'garden')
})

test('filters non-series and duplicate IDs while bounding result count', async () => {
  const first = series('same', '同名节目')
  const entries = await buildSeriesIndex([
    first,
    series('same', '另一名称'),
    series('movie', '电影', { sourceType: 'Movie' }),
    series('', '空标识'),
    ...Array.from({ length: 60 }, (_, index) => series(`series-${index}`, `节目 ${index}`)),
  ])
  assert.equal(entries.length, 61)
  assert.equal(entries[0].item, first)
  assert.equal(searchSeries(entries, '', 200).length, 50)
  assert.equal(searchSeries(entries, '', 0).length, 1)
  assert.equal(searchSeries(entries, '', 2.6).length, 3)
})

test('reflects replacement objects after metadata, favorite or session changes', async () => {
  const original = series('same-id', '旧标题')
  await buildSeriesIndex([original])
  const replacement = { ...original, title: '新标题', favorite: true }
  const entries = await buildSeriesIndex([replacement])
  assert.equal(searchSeries(entries, '新标题')[0].item.favorite, true)
  assert.deepEqual(searchSeries(entries, '旧标题'), [])
})

test('rejects an already cancelled build', async () => {
  const controller = new AbortController()
  controller.abort()
  await assert.rejects(buildSeriesIndex([series('sea', '深海回声')], controller.signal), {
    name: 'AbortError',
  })
})

test('yields to the event loop and stops indexing when the page leaves', async () => {
  let visited = 0
  const items = Array.from({ length: 10_000 }, (_, index) => ({
    ...series(`cancel-${index}`, ''),
    get title() {
      visited += 1
      return `深海回声与以太花园 第${index}部`
    },
  }))
  const controller = new AbortController()
  const cancel = setTimeout(() => controller.abort(), 0)
  try {
    await assert.rejects(buildSeriesIndex(items, controller.signal), { name: 'AbortError' })
    assert.ok(visited < items.length, 'leaving search must not finish the remaining library')
  } finally {
    clearTimeout(cancel)
  }
})
