import { t } from '../../SharedUI/i18n.mjs'
import type { MediaItem } from './data'

const ticksPerSecond = 10_000_000

export function resumeProgress(item?: MediaItem) {
  const ticks = item?.playbackPositionTicks ?? 0
  const duration = item?.runtimeTicks ?? 0
  return Number.isFinite(ticks) && Number.isFinite(duration) && duration > 0
    ? Math.min(100, Math.max(0, ticks / duration * 100))
    : Math.min(100, Math.max(0, item?.progress || 0))
}

export function watchedTime(item?: MediaItem) {
  const ticks = item?.playbackPositionTicks ?? 0
  const seconds = Number.isFinite(ticks) ? Math.max(0, Math.floor(ticks / ticksPerSecond)) : 0
  return t("{0} 分 {1} 秒", { 0: Math.floor(seconds / 60), 1: String(seconds % 60).padStart(2, '0') })
}

export function latestWatchedEpisode(episodes: MediaItem[]) {
  const watched = episodes.filter((episode) => episode.lastPlayedDate || (episode.playbackPositionTicks ?? 0) > 0 || episode.watched)
  return watched.reduce<MediaItem | undefined>((latest, episode) => {
    if (!latest) return episode
    const date = Date.parse(episode.lastPlayedDate ?? '') || 0
    const latestDate = Date.parse(latest.lastPlayedDate ?? '') || 0
    if (date !== latestDate) return date > latestDate ? episode : latest
    // Older servers may omit dates. Prefer resumable items, then later episodes.
    const resumable = Number((episode.playbackPositionTicks ?? 0) > 0)
    const latestResumable = Number((latest.playbackPositionTicks ?? 0) > 0)
    return resumable !== latestResumable ? resumable > latestResumable ? episode : latest : episode
  }, undefined)
}

// Scoped to a JellyfinClient/account; never persisted. Retains precise short-play
// positions even when the server's minimum resume threshold clears them.
export class WatchProgress {
  private items = new Map<string, MediaItem>()
  private pending = new Set<Promise<unknown>>()
  private confirmedAt = new WeakMap<MediaItem, number>()

  record(item: MediaItem, positionTicks: number, durationTicks: number) {
    if (!Number.isFinite(positionTicks) || positionTicks < 0) return
    const duration = durationTicks > 0 ? durationTicks : item.runtimeTicks
    const position = duration ? Math.min(positionTicks, duration) : positionTicks
    const completed = Boolean(duration && position >= duration)
    const next = { ...item, get subtitle() { return item.subtitle }, get duration() { return item.duration }, runtimeTicks: duration, playbackPositionTicks: completed ? 0 : position,
      watched: completed || Boolean(item.watched), lastPlayedDate: new Date().toISOString() }
    next.progress = completed ? undefined : resumeProgress(next)
    this.items.delete(item.id)
    this.items.set(item.id, next)
    if (this.items.size > 64) this.items.delete(this.items.keys().next().value!)
    return next
  }

  confirm(record: MediaItem) {
    if (this.items.get(record.id) === record) this.confirmedAt.set(record, Date.now())
  }

  patch(item: MediaItem) {
    const local = this.items.get(item.id)
    if (!local || (Date.parse(item.lastPlayedDate ?? '') || 0) > (this.confirmedAt.get(local) ?? Date.parse(local.lastPlayedDate!))) return item
    return { ...item, get subtitle() { return item.subtitle }, get duration() { return item.duration }, runtimeTicks: local.runtimeTicks, playbackPositionTicks: local.playbackPositionTicks,
      progress: local.progress, watched: Boolean(item.watched || local.watched), lastPlayedDate: local.lastPlayedDate }
  }

  latestFor(item: MediaItem) {
    return latestWatchedEpisode([...this.items.values()].filter((candidate) =>
      candidate.sourceType === 'Episode' && (candidate.id === item.id
        || candidate.seriesId === (item.seriesId ?? item.id))))
  }

  forget(itemId: string) { this.items.delete(itemId) }

  async track<T>(request: Promise<T>): Promise<T> {
    this.pending.add(request)
    try { return await request } finally { this.pending.delete(request) }
  }

  async settle() { await Promise.allSettled([...this.pending]) }
}
