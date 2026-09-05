import assert from 'node:assert/strict'
import { loadSeriesSearch } from './loadSeriesSearch.mjs'

const names = ['深海回声', '以太花园', '玻璃信号', '北境线', '白噪之城', '雨幕档案', '薄膜宇宙', '苍蓝之后', '星际漫游', '云端笔记']
const english = ['Echoes of the Abyss', 'Aether Garden', 'Glass Signal', 'North Line', 'White Noise', 'Rain Archive', 'Membrane', 'Pale Blue', 'Star Voyage', 'Cloud Notes']
const items = Array.from({ length: 10_000 }, (_, index) => ({
  id: `synthetic-${index}`,
  title: `${names[index % names.length]} ${Math.floor(index / 10)}`,
  original: `${english[index % english.length]} ${Math.floor(index / 10)}`,
  aliases: [index % 2 ? '未来物语' : '远方来信'],
  subtitle: '', kind: '剧集', art: index % 12, sourceType: 'Series',
  favorite: index % 17 === 0, progress: index % 19 === 0 ? 12 : undefined,
  rating: String(7 + index % 3), dateCreated: `2025-01-${String(index % 28 + 1).padStart(2, '0')}`,
}))
const queries = ['', 'sh', 'shhs', 'shenhai', 'echoes', 'snhi', 'ＡｅＴｈｅｒ', 'ÉCHOES', '玻璃', 'yflx', 'weilaiwuyu', 'shhs s02e03', '北境线 ep12', 'zzzzzzzz', 'rain', 'star']
const timedQueries = [...queries]
let random = 42
for (let index = 0; index < 184; index++) {
  random = Math.imul(random, 1664525) + 1013904223 | 0
  const title = english[(random >>> 0) % english.length].toLowerCase()
  queries.push(title.slice(0, 1 + ((random >>> 6) % title.length)))
}

const implementations = {}
if (process.argv[2]) implementations.before = await loadSeriesSearch(process.argv[2])
implementations.after = await loadSeriesSearch()
const indexes = {}
const metrics = {}
for (const [name, api] of Object.entries(implementations)) {
  // Isolate indexing from the first pinyin module import.
  await api.buildSeriesIndex([])
  let previous = performance.now()
  let maxGap = 0
  let yields = 0
  const timer = setInterval(() => {
    const now = performance.now()
    maxGap = Math.max(maxGap, now - previous)
    previous = now
    yields++
  }, 1)
  const start = performance.now()
  try {
    indexes[name] = await api.buildSeriesIndex(items)
  } finally {
    maxGap = Math.max(maxGap, performance.now() - previous)
    clearInterval(timer)
  }
  const coldBuildMs = performance.now() - start
  const repeatStart = performance.now()
  await api.buildSeriesIndex(items)
  metrics[name] = { coldBuildMs, repeatBuildMs: performance.now() - repeatStart, maxEventLoopGapMs: maxGap, yields }

  for (const query of timedQueries) api.searchSeries(indexes[name], query, 24)
  const durations = []
  for (let run = 0; run < 7; run++) {
    for (const query of timedQueries) {
      const queryStart = performance.now()
      api.searchSeries(indexes[name], query, 24)
      durations.push(performance.now() - queryStart)
    }
  }
  durations.sort((a, b) => a - b)
  metrics[name].queryMedianMs = durations[Math.floor(durations.length / 2)]
  metrics[name].queryP95Ms = durations[Math.floor(durations.length * .95)]
}

let equivalentComparisons = 0
if (implementations.before) {
  assert.deepEqual(indexes.after, indexes.before)
  for (const query of queries) {
    for (const limit of [1, 24, 50]) {
      const priority = ['synthetic-84', 'synthetic-15', 'synthetic-323']
      assert.deepEqual(
        implementations.after.searchSeries(indexes.after, query, limit, priority),
        implementations.before.searchSeries(indexes.before, query, limit, priority),
        query,
      )
      equivalentComparisons++
    }
  }
}
console.log(JSON.stringify({
  runtime: `${process.version} ${process.platform} ${process.arch}`,
  fixtureItems: items.length,
  equivalentComparisons,
  metrics,
}, null, 2))
