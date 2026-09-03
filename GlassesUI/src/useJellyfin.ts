import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { MediaItem } from './data'
import { JellyfinClient, type CatalogSnapshot } from './jellyfin'
import {
  discoverRuntime,
  subscribeRuntime,
  type RuntimeBootstrap,
} from './runtime'

export type JellyfinUiStatus = 'booting' | 'loading' | 'ready' | 'no-session' | 'error'

function patchCatalogItem(
  snapshot: CatalogSnapshot,
  itemId: string,
  values: Partial<MediaItem>,
) {
  const patch = (item: MediaItem) => item.id === itemId ? { ...item, ...values } : item
  return {
    ...snapshot,
    featured: patch(snapshot.featured),
    libraries: snapshot.libraries.map(patch),
    allItems: snapshot.allItems.map(patch),
    favorites: snapshot.favorites.map(patch),
    shelves: snapshot.shelves.map((shelf) => ({
      ...shelf,
      items: shelf.items.map(patch),
    })),
  }
}

export function useJellyfin() {
  const [runtime, setRuntime] = useState<RuntimeBootstrap | null>(null)
  const [snapshot, setSnapshot] = useState<CatalogSnapshot | null>(null)
  const [status, setStatus] = useState<JellyfinUiStatus>('booting')
  const [error, setError] = useState('')
  const [refreshing, setRefreshing] = useState(false)
  const loadGeneration = useRef(0)
  const snapshotRef = useRef<CatalogSnapshot | null>(null)

  useEffect(() => {
    snapshotRef.current = snapshot
  }, [snapshot])

  useEffect(() => {
    let disposed = false
    const apply = (next: RuntimeBootstrap) => {
      if (!disposed) setRuntime(next)
    }
    const unsubscribe = subscribeRuntime(apply)
    discoverRuntime().then(apply)
    return () => {
      disposed = true
      unsubscribe()
    }
  }, [])

  const session = runtime?.session ?? null
  const sessionKey = session
    ? `${session.serverUrl}\n${session.userId}\n${session.accessToken}\n${session.deviceId}`
    : ''
  const client = useMemo(
    () => session ? new JellyfinClient(session) : null,
    // The key deliberately includes the access token so a phone-side relogin replaces the client.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [sessionKey],
  )

  const loadCatalog = useCallback(async (background = false) => {
    if (!client) return false
    const generation = ++loadGeneration.current
    if (background) setRefreshing(true)
    else {
      setStatus('loading')
      setSnapshot(null)
    }
    setError('')

    try {
      const next = await client.loadHome()
      if (generation !== loadGeneration.current) return false
      setSnapshot(next)
      setStatus('ready')
      return true
    } catch (reason) {
      if (generation !== loadGeneration.current) return false
      setError(reason instanceof Error ? reason.message : '媒体库加载失败。')
      if (!background || !snapshotRef.current) setStatus('error')
      return false
    } finally {
      if (generation === loadGeneration.current) setRefreshing(false)
    }
  }, [client])

  useEffect(() => {
    loadGeneration.current += 1
    if (!runtime) {
      setStatus('booting')
      return
    }
    if (!client) {
      setSnapshot(null)
      setError(runtime.error ?? '')
      setStatus(runtime.source === 'android' && !runtime.error ? 'no-session' : 'error')
      return
    }
    void loadCatalog()
  }, [client, loadCatalog, runtime])

  const loadFolder = useCallback(async (parentId: string) => {
    if (!client) return []
    return client.loadFolder(parentId)
  }, [client])

  const search = useCallback(async (term: string) => {
    if (!client) return []
    return client.search(term)
  }, [client])

  const loadDetail = useCallback(async (itemId: string, seasonId?: string) => {
    if (!client) throw new Error('Jellyfin 会话不可用。')
    return client.loadDetail(itemId, seasonId)
  }, [client])

  const setFavorite = useCallback(async (item: MediaItem, favorite: boolean) => {
    if (!client) return false
    await client.setFavorite(item.id, favorite)
    setSnapshot((current) => {
      if (!current) return current
      const patchedItem = { ...item, favorite }
      const patched = patchCatalogItem(current, item.id, { favorite })
      return {
        ...patched,
        favorites: favorite
          ? [patchedItem, ...patched.favorites.filter((candidate) => candidate.id !== item.id)]
          : patched.favorites.filter((candidate) => candidate.id !== item.id),
      }
    })
    return true
  }, [client])

  const setPlayed = useCallback(async (item: MediaItem, played: boolean) => {
    if (!client) return false
    await client.setPlayed(item.id, played)
    setSnapshot((current) => current
      ? patchCatalogItem(current, item.id, {
          watched: played,
          progress: played ? undefined : item.progress,
          playbackPositionTicks: played ? 0 : item.playbackPositionTicks,
        })
      : current)
    return true
  }, [client])

  const refresh = useCallback(() => loadCatalog(true), [loadCatalog])
  const retry = useCallback(() => loadCatalog(false), [loadCatalog])

  return {
    runtime,
    snapshot,
    status,
    error,
    refreshing,
    refresh,
    retry,
    loadFolder,
    search,
    loadDetail,
    setFavorite,
    setPlayed,
  }
}
