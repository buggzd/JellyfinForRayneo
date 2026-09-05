import type { MediaItem } from './data'

type PinyinConverter = typeof import('pinyin-pro')['pinyin']

export type SeriesIndexEntry = {
  item: MediaItem
  titles: string[]
  compactTitles: string[]
  fullPinyin: string[]
  pinyinInitials: string[]
}

export type SeriesSearchMatch = {
  item: MediaItem
  reason: '标题' | '首字母' | '拼音' | '模糊匹配' | '推荐'
  score: number
}

export type ParsedSeriesQuery = {
  term: string
  seasonHint?: number
  episodeHint?: number
}

const letterOrNumber = /[\p{Letter}\p{Number}]/u
const titleCollator = new Intl.Collator('zh-CN')
const cachedEntries = new WeakMap<MediaItem, SeriesIndexEntry>()
const indexWorkBudgetMs = 8

function normalizeWords(value: string) {
  return value
    .normalize('NFKD')
    .replace(/\p{Mark}+/gu, '')
    .toLocaleLowerCase()
    .replace(/[^\p{Letter}\p{Number}]+/gu, ' ')
    .trim()
}

function compact(value: string) {
  return normalizeWords(value).replace(/\s+/g, '')
}

function unique(values: string[]) {
  return Array.from(new Set(values.filter(Boolean)))
}

function romanized(value: string, initials: boolean, convert: PinyinConverter) {
  return compact(convert(value, {
    pattern: initials ? 'first' : 'pinyin',
    toneType: 'none',
    type: 'array',
    nonZh: 'consecutive',
    v: true,
  }).join(' '))
}

function indexEntry(item: MediaItem, convert: PinyinConverter): SeriesIndexEntry {
  const sourceTitles = unique([
    item.title,
    item.original ?? '',
    item.sortName ?? '',
    ...(item.aliases ?? []),
  ])

  return {
    item,
    titles: unique(sourceTitles.map(normalizeWords)),
    compactTitles: unique(sourceTitles.map(compact)),
    fullPinyin: unique(sourceTitles.map((title) => romanized(title, false, convert))),
    pinyinInitials: unique(sourceTitles.map((title) => romanized(title, true, convert))),
  }
}

export async function buildSeriesIndex(items: MediaItem[], signal?: AbortSignal) {
  const checkAborted = () => {
    if (signal?.aborted) throw new DOMException('Series index build aborted', 'AbortError')
  }
  checkAborted()
  const { pinyin } = await import('pinyin-pro')
  checkAborted()
  const seen = new Set<string>()
  const entries: SeriesIndexEntry[] = []
  let sliceStarted = performance.now()
  for (const item of items) {
    if (item.sourceType !== 'Series' || !item.id || seen.has(item.id)) continue
    seen.add(item.id)
    let entry = cachedEntries.get(item)
    if (!entry) {
      entry = indexEntry(item, pinyin)
      cachedEntries.set(item, entry)
    }
    entries.push(entry)

    // Keep animation and remote input responsive while romanizing a large library.
    // Weak keys let session replacement release the old titles and image URLs.
    if (performance.now() - sliceStarted >= indexWorkBudgetMs) {
      await new Promise<void>((resolve) => setTimeout(resolve, 0))
      checkAborted()
      sliceStarted = performance.now()
    }
  }
  return entries
}

export function parseSeriesQuery(value: string): ParsedSeriesQuery {
  const trimmed = value.trim().slice(0, 48)
  if (!trimmed) return { term: '' }

  const seasonEpisode = trimmed.match(/^(.*?)\s+s(\d{1,3})\s*e(\d{1,4})$/i)
  if (seasonEpisode?.[1]?.trim()) {
    return {
      term: seasonEpisode[1].trim(),
      seasonHint: Number(seasonEpisode[2]),
      episodeHint: Number(seasonEpisode[3]),
    }
  }

  const episode = trimmed.match(/^(.*?)\s+(?:e|ep)?(\d{1,4})$/i)
  if (episode?.[1]?.trim() && letterOrNumber.test(episode[1])) {
    return {
      term: episode[1].trim(),
      episodeHint: Number(episode[2]),
    }
  }

  return { term: trimmed }
}

function preferenceScore(item: MediaItem) {
  return (item.favorite ? 180 : 0)
    + (item.progress ? 150 : 0)
    + (item.unwatched ? Math.min(120, 45 + item.unwatched) : 0)
    + Math.min(100, Number(item.rating ?? 0) * 8)
}

function subsequencePenalty(needle: string, target: string) {
  let targetIndex = 0
  let firstMatch = -1
  let previousMatch = -1
  let gaps = 0

  for (const character of needle) {
    const match = target.indexOf(character, targetIndex)
    if (match < 0) return null
    if (firstMatch < 0) firstMatch = match
    if (previousMatch >= 0) gaps += match - previousMatch - 1
    previousMatch = match
    targetIndex = match + 1
  }

  return firstMatch * 3 + gaps + Math.max(0, target.length - needle.length) * .05
}

function bestScore(values: string[], query: string, scores: {
  exact: number
  prefix: number
  contains: number
}) {
  let best = Number.NEGATIVE_INFINITY
  for (const value of values) {
    if (value === query) best = Math.max(best, scores.exact)
    else if (value.startsWith(query)) best = Math.max(best, scores.prefix - Math.min(120, value.length - query.length))
    else {
      const position = value.indexOf(query)
      if (position >= 0) best = Math.max(best, scores.contains - position * 8)
    }
  }
  return best
}

function scoreEntry(
  entry: SeriesIndexEntry,
  words: string,
  query: string,
  priorityBoost: number,
): Omit<SeriesSearchMatch, 'item'> | null {
  if (!query) return { reason: '推荐', score: preferenceScore(entry.item) + priorityBoost }

  const titleScore = Math.max(
    bestScore(entry.titles, words, { exact: 12_000, prefix: 11_300, contains: 8_400 }),
    bestScore(entry.compactTitles, query, { exact: 11_900, prefix: 11_200, contains: 8_300 }),
  )
  const initialsScore = bestScore(entry.pinyinInitials, query, {
    exact: 10_900,
    prefix: 10_300,
    contains: 7_700,
  })
  const pinyinScore = bestScore(entry.fullPinyin, query, {
    exact: 10_700,
    prefix: 10_100,
    contains: 7_500,
  })

  let score = titleScore
  let reason: SeriesSearchMatch['reason'] = '标题'
  if (initialsScore > score) {
    score = initialsScore
    reason = '首字母'
  }
  if (pinyinScore > score) {
    score = pinyinScore
    reason = '拼音'
  }

  if (!Number.isFinite(score) && query.length >= 2) {
    const penalties = [...entry.pinyinInitials, ...entry.fullPinyin, ...entry.compactTitles]
      .map((candidate) => subsequencePenalty(query, candidate))
      .filter((penalty): penalty is number => penalty !== null)
    if (penalties.length) {
      score = 5_600 - Math.min(...penalties) * 16
      reason = '模糊匹配'
    }
  }

  if (!Number.isFinite(score)) return null
  return { reason, score: score + preferenceScore(entry.item) + priorityBoost }
}

export function searchSeries(
  entries: SeriesIndexEntry[],
  query: string,
  limit = 12,
  prioritySeriesIds: readonly string[] = [],
): SeriesSearchMatch[] {
  const parsed = parseSeriesQuery(query)
  const words = normalizeWords(parsed.term)
  const compactQuery = words.replace(/\s+/g, '')
  const boundedLimit = Math.max(1, Math.min(50, Math.round(limit)))
  const priority = new Map(prioritySeriesIds.map((id, index) => [
    id,
    Math.max(80, 360 - index * 24),
  ]))

  return entries
    .map((entry) => {
      const match = scoreEntry(entry, words, compactQuery, priority.get(entry.item.id) ?? 0)
      return match ? { item: entry.item, ...match } : null
    })
    .filter((match): match is SeriesSearchMatch => match !== null)
    .sort((left, right) => (
      right.score - left.score
      || Number(right.item.favorite) - Number(left.item.favorite)
      || (Date.parse(right.item.dateCreated ?? '') || 0) - (Date.parse(left.item.dateCreated ?? '') || 0)
      || titleCollator.compare(left.item.title, right.item.title)
    ))
    .slice(0, boundedLimit)
}
