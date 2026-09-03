import type { MediaItem, MediaKind, MediaShelf } from './data'
import type { JellyfinSession } from './runtime'

type JellyfinUserData = {
  PlaybackPositionTicks?: number
  PlayedPercentage?: number
  UnplayedItemCount?: number
  IsFavorite?: boolean
  Played?: boolean
}

type JellyfinMediaStream = {
  Type?: string
  Codec?: string
  DisplayTitle?: string
  Width?: number
  Height?: number
  BitRate?: number
  Channels?: number
  Language?: string
  IsDefault?: boolean
  IsExternal?: boolean
  Index?: number
}

type JellyfinMediaSource = {
  Container?: string
  Bitrate?: number
  RunTimeTicks?: number
  MediaStreams?: JellyfinMediaStream[]
}

export type JellyfinItemDto = {
  Id?: string
  Name?: string
  OriginalTitle?: string
  Type?: string
  MediaType?: string
  CollectionType?: string
  IsFolder?: boolean
  ParentId?: string
  SeriesId?: string
  SeasonId?: string
  SeriesName?: string
  SeasonName?: string
  IndexNumber?: number
  ParentIndexNumber?: number
  ChildCount?: number
  RunTimeTicks?: number
  ProductionYear?: number
  PremiereDate?: string
  DateCreated?: string
  CommunityRating?: number
  OfficialRating?: string
  Overview?: string
  Taglines?: string[]
  Genres?: string[]
  Path?: string
  ImageTags?: Record<string, string>
  BackdropImageTags?: string[]
  SeriesPrimaryImageTag?: string
  UserData?: JellyfinUserData
  Studios?: Array<{ Name?: string }>
  People?: Array<{ Name?: string; Role?: string; Type?: string }>
  MediaSources?: JellyfinMediaSource[]
}

type JellyfinItemsResponse = {
  Items?: JellyfinItemDto[]
  TotalRecordCount?: number
}

export type CatalogSnapshot = {
  featured: MediaItem
  shelves: MediaShelf[]
  libraries: MediaItem[]
  allItems: MediaItem[]
  favorites: MediaItem[]
}

export type DetailSnapshot = {
  item: MediaItem
  seriesId?: string
  selectedSeasonId?: string
  seasons: MediaItem[]
  episodes: MediaItem[]
  similar: MediaItem[]
  extras: MediaItem[]
}

const itemFields = [
  'Overview',
  'Genres',
  'Studios',
  'People',
  'MediaSources',
  'PrimaryImageAspectRatio',
  'DateCreated',
  'ProductionYear',
  'CommunityRating',
  'OfficialRating',
  'RunTimeTicks',
  'UserData',
  'Path',
  'Taglines',
].join(',')

function hash(value: string) {
  let result = 2166136261
  for (let index = 0; index < value.length; index += 1) {
    result ^= value.charCodeAt(index)
    result = Math.imul(result, 16777619)
  }
  return Math.abs(result >>> 0)
}

function padEpisode(value: number | undefined) {
  return value === undefined ? '--' : String(value).padStart(2, '0')
}

function formatDuration(ticks: number | undefined) {
  if (!ticks || ticks <= 0) return undefined
  const minutes = Math.max(1, Math.round(ticks / 600_000_000))
  const hours = Math.floor(minutes / 60)
  const remainder = minutes % 60
  if (!hours) return `${minutes} 分钟`
  if (!remainder) return `${hours} 小时`
  return `${hours} 小时 ${String(remainder).padStart(2, '0')} 分`
}

function mediaKind(item: JellyfinItemDto): MediaKind {
  switch (item.Type) {
    case 'Movie':
      return '电影'
    case 'Series':
    case 'Episode':
    case 'Season':
      return '剧集'
    case 'BoxSet':
    case 'Playlist':
    case 'MusicAlbum':
      return '合集'
    case 'CollectionFolder':
    case 'Folder':
    case 'UserView':
      return '文件夹'
    default:
      return '视频'
  }
}

function libraryLabel(collectionType: string | undefined) {
  switch (collectionType?.toLocaleLowerCase()) {
    case 'movies':
      return '电影库'
    case 'tvshows':
      return '剧集库'
    case 'music':
      return '音乐库'
    case 'homevideos':
      return '家庭视频'
    case 'photos':
      return '照片库'
    case 'musicvideos':
      return '音乐视频'
    default:
      return '媒体库'
  }
}

function itemSubtitle(item: JellyfinItemDto) {
  if (item.Type === 'Episode') {
    return `S${padEpisode(item.ParentIndexNumber)} E${padEpisode(item.IndexNumber)} · ${item.Name ?? item.SeasonName ?? '剧集'}`
  }
  if (item.Type === 'Series') {
    const count = item.ChildCount ? `${item.ChildCount} 集` : '剧集'
    return [count, ...(item.Genres ?? []).slice(0, 2)].join(' · ')
  }
  if (item.Type === 'CollectionFolder' || item.Type === 'Folder' || item.Type === 'UserView') {
    return `${libraryLabel(item.CollectionType)}${item.ChildCount ? ` · ${item.ChildCount} 项` : ''}`
  }

  const facts = [
    item.ProductionYear ? String(item.ProductionYear) : '',
    formatDuration(item.RunTimeTicks) ?? '',
    ...(item.Genres ?? []).slice(0, 1),
  ].filter(Boolean)
  return facts.join(' · ') || mediaKind(item)
}

function resolutionFor(source: JellyfinMediaSource | undefined) {
  const video = source?.MediaStreams?.find((stream) => stream.Type === 'Video')
  const width = video?.Width ?? 0
  const height = video?.Height ?? 0
  if (width >= 3800 || height >= 2100) return '4K'
  if (width >= 1900 || height >= 1060) return '1080P'
  if (width >= 1260 || height >= 700) return '720P'
  return video?.DisplayTitle?.split(' ')[0]
}

function progressFor(item: JellyfinItemDto) {
  const userData = item.UserData
  if (!userData || userData.Played) return undefined
  if (typeof userData.PlayedPercentage === 'number' && userData.PlayedPercentage > 0) {
    return Math.min(99, Math.max(1, Math.round(userData.PlayedPercentage)))
  }
  if (!item.RunTimeTicks || !userData.PlaybackPositionTicks) return undefined
  return Math.min(99, Math.max(1, Math.round(
    userData.PlaybackPositionTicks / item.RunTimeTicks * 100,
  )))
}

function unique(items: MediaItem[]) {
  const seen = new Set<string>()
  return items.filter((item) => {
    if (seen.has(item.id)) return false
    seen.add(item.id)
    return true
  })
}

export class JellyfinClient {
  readonly session: JellyfinSession

  constructor(session: JellyfinSession) {
    this.session = session
  }

  private url(path: string, query: Record<string, string | number | boolean | undefined> = {}) {
    const base = `${this.session.serverUrl}/${path.replace(/^\/+/, '')}`
    const url = new URL(base)
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== '') url.searchParams.set(key, String(value))
    })
    return url.toString()
  }

  private async request<T>(
    path: string,
    query: Record<string, string | number | boolean | undefined> = {},
    init: RequestInit = {},
  ): Promise<T> {
    const headers = new Headers(init.headers)
    headers.set('Accept', 'application/json')
    headers.set('X-Emby-Token', this.session.accessToken)
    headers.set(
      'X-Emby-Authorization',
      `MediaBrowser Client="Lucent for RayNeo", Device="RayNeo Air", DeviceId="${this.session.deviceId}", Version="0.1.0", Token="${this.session.accessToken}"`,
    )
    if (init.body) headers.set('Content-Type', 'application/json')

    const response = await fetch(this.url(path, query), { ...init, headers, cache: 'no-store' })
    if (!response.ok) {
      let message = ''
      try {
        message = (await response.text()).trim()
      } catch {
        message = ''
      }
      throw new Error(message || `Jellyfin 请求失败（${response.status}）。`)
    }
    if (response.status === 204 || response.headers.get('Content-Length') === '0') {
      return undefined as T
    }
    return response.json() as Promise<T>
  }

  private imageUrl(
    itemId: string,
    type: 'Primary' | 'Backdrop' | 'Logo',
    tag?: string,
    wide = false,
  ) {
    return this.url(`/Items/${encodeURIComponent(itemId)}/Images/${type}`, {
      tag,
      maxWidth: wide ? 1920 : 720,
      maxHeight: wide ? 1080 : 1080,
      quality: 88,
      api_key: this.session.accessToken,
    })
  }

  mapItem = (source: JellyfinItemDto): MediaItem => {
    const id = source.Id ?? `missing-${hash(source.Name ?? 'item')}`
    const sourceName = source.Name?.trim() || '未命名媒体'
    const title = source.Type === 'Episode' && source.SeriesName?.trim()
      ? source.SeriesName.trim()
      : sourceName
    const mediaSource = source.MediaSources?.[0]
    const video = mediaSource?.MediaStreams?.find((stream) => stream.Type === 'Video')
    const audio = mediaSource?.MediaStreams?.find((stream) => stream.Type === 'Audio')
    const primaryOwner = source.ImageTags?.Primary
      ? id
      : source.SeriesPrimaryImageTag && source.SeriesId
        ? source.SeriesId
        : ''
    const primaryTag = source.ImageTags?.Primary ?? source.SeriesPrimaryImageTag
    const backdropTag = source.BackdropImageTags?.[0]
    const progress = progressFor(source)
    const playableTypes = ['Movie', 'Episode', 'Video', 'MusicVideo', 'Audio']

    return {
      id,
      title,
      original: source.Type === 'Episode' && sourceName !== title
        ? sourceName
        : source.OriginalTitle?.trim() || undefined,
      subtitle: itemSubtitle(source),
      kind: mediaKind(source),
      year: source.ProductionYear ? String(source.ProductionYear) : undefined,
      duration: formatDuration(source.RunTimeTicks ?? mediaSource?.RunTimeTicks),
      rating: typeof source.CommunityRating === 'number'
        ? source.CommunityRating.toFixed(1)
        : undefined,
      progress,
      art: hash(id) % 12,
      favorite: Boolean(source.UserData?.IsFavorite),
      watched: Boolean(source.UserData?.Played),
      unwatched: source.UserData?.UnplayedItemCount || undefined,
      folder: ['CollectionFolder', 'Folder', 'UserView'].includes(source.Type ?? ''),
      resolution: resolutionFor(mediaSource),
      overview: source.Overview?.trim() || undefined,
      tagline: source.Taglines?.find(Boolean)?.trim() || undefined,
      officialRating: source.OfficialRating?.trim() || undefined,
      genres: source.Genres ?? [],
      studios: (source.Studios ?? []).map((studio) => studio.Name ?? '').filter(Boolean),
      people: (source.People ?? []).map((person) => ({
        name: person.Name ?? '',
        role: person.Role ?? '',
        type: person.Type ?? '',
      })).filter((person) => person.name),
      path: source.Path,
      dateCreated: source.DateCreated,
      sourceType: source.Type,
      mediaType: source.MediaType,
      collectionType: source.CollectionType,
      parentId: source.ParentId,
      seriesId: source.SeriesId,
      seasonId: source.SeasonId,
      indexNumber: source.IndexNumber,
      parentIndexNumber: source.ParentIndexNumber,
      runtimeTicks: source.RunTimeTicks ?? mediaSource?.RunTimeTicks,
      playbackPositionTicks: source.UserData?.PlaybackPositionTicks,
      imageUrl: primaryOwner && primaryTag
        ? this.imageUrl(primaryOwner, 'Primary', primaryTag)
        : undefined,
      backdropUrl: backdropTag
        ? this.imageUrl(id, 'Backdrop', backdropTag, true)
        : primaryOwner && primaryTag
          ? this.imageUrl(primaryOwner, 'Primary', primaryTag, true)
          : undefined,
      logoUrl: source.ImageTags?.Logo
        ? this.imageUrl(id, 'Logo', source.ImageTags.Logo, true)
        : undefined,
      videoCodec: video?.Codec?.toLocaleUpperCase(),
      audioCodec: audio?.Codec?.toLocaleUpperCase(),
      container: mediaSource?.Container?.toLocaleUpperCase(),
      width: video?.Width,
      height: video?.Height,
      bitrate: mediaSource?.Bitrate ?? video?.BitRate,
      canPlay: playableTypes.includes(source.Type ?? '') || source.MediaType === 'Video',
    }
  }

  async loadHome(): Promise<CatalogSnapshot> {
    const common = {
      Fields: itemFields,
      ImageTypeLimit: 1,
      EnableImageTypes: 'Primary,Backdrop,Logo',
    }
    const userId = encodeURIComponent(this.session.userId)
    const [viewsResponse, resumeResponse, latestResponse, nextUpResponse, allResponse, favoriteResponse] = await Promise.all([
      this.request<JellyfinItemsResponse>(`/Users/${userId}/Views`, common),
      this.request<JellyfinItemsResponse>(`/Users/${userId}/Items/Resume`, {
        ...common,
        Recursive: true,
        MediaTypes: 'Video',
        Limit: 12,
      }),
      this.request<JellyfinItemDto[]>(`/Users/${userId}/Items/Latest`, {
        ...common,
        IncludeItemTypes: 'Movie,Series,Episode,Video',
        Limit: 14,
      }),
      this.request<JellyfinItemsResponse>('/Shows/NextUp', {
        ...common,
        UserId: this.session.userId,
        Limit: 12,
      }),
      this.request<JellyfinItemsResponse>(`/Users/${userId}/Items`, {
        ...common,
        Recursive: true,
        IncludeItemTypes: 'Movie,Series,Video',
        SortBy: 'DateCreated,SortName',
        SortOrder: 'Descending',
        Limit: 240,
      }),
      this.request<JellyfinItemsResponse>(`/Users/${userId}/Items`, {
        ...common,
        Recursive: true,
        IncludeItemTypes: 'Movie,Series,Episode,Video',
        Filters: 'IsFavorite',
        SortBy: 'SortName',
        Limit: 240,
      }),
    ])

    const libraries = (viewsResponse.Items ?? []).map(this.mapItem)
    const resume = (resumeResponse.Items ?? []).map(this.mapItem)
    const latest = (latestResponse ?? []).map(this.mapItem)
    const nextUp = (nextUpResponse.Items ?? []).map(this.mapItem)
    const allItems = (allResponse.Items ?? []).map(this.mapItem)
    const favorites = (favoriteResponse.Items ?? []).map(this.mapItem)
    const playable = unique([...resume, ...nextUp, ...latest, ...allItems]).filter((item) => item.canPlay || item.sourceType === 'Series')
    const featured = playable[0] ?? allItems[0] ?? libraries[0] ?? {
      id: 'empty-library',
      title: this.session.serverName || 'Jellyfin',
      subtitle: '媒体库暂无可显示内容',
      kind: '文件夹',
      art: 0,
      folder: true,
      overview: '请在 Jellyfin 服务器中添加媒体，然后刷新页面。',
    }
    const shelves: MediaShelf[] = [
      { id: 'libraries', title: '我的媒体', eyebrow: 'LIBRARIES', items: libraries, library: true },
      { id: 'resume', title: '继续观看', eyebrow: 'RESUME', items: resume },
      { id: 'next-up', title: '下一集', eyebrow: 'UP NEXT', items: nextUp },
      { id: 'latest', title: '最近添加', eyebrow: 'JUST IN', items: latest },
      { id: 'all', title: '探索媒体库', eyebrow: 'DISCOVER', items: allItems.slice(0, 14) },
    ].filter((shelf) => shelf.items.length)

    return { featured, shelves, libraries, allItems, favorites }
  }

  async loadFolder(parentId: string) {
    const response = await this.request<JellyfinItemsResponse>(
      `/Users/${encodeURIComponent(this.session.userId)}/Items`,
      {
        ParentId: parentId,
        Fields: itemFields,
        ImageTypeLimit: 1,
        EnableImageTypes: 'Primary,Backdrop,Logo',
        SortBy: 'SortName',
        SortOrder: 'Ascending',
        Limit: 500,
      },
    )
    return (response.Items ?? []).map(this.mapItem)
  }

  async search(term: string) {
    const needle = term.trim()
    if (!needle) return []
    const response = await this.request<JellyfinItemsResponse>(
      `/Users/${encodeURIComponent(this.session.userId)}/Items`,
      {
        SearchTerm: needle,
        Recursive: true,
        IncludeItemTypes: 'Movie,Series,Episode,Video,BoxSet',
        Fields: itemFields,
        ImageTypeLimit: 1,
        EnableImageTypes: 'Primary,Backdrop,Logo',
        Limit: 100,
      },
    )
    return (response.Items ?? []).map(this.mapItem)
  }

  async loadDetail(itemId: string, requestedSeasonId?: string): Promise<DetailSnapshot> {
    const userId = encodeURIComponent(this.session.userId)
    const encodedItemId = encodeURIComponent(itemId)
    const detail = await this.request<JellyfinItemDto>(
      `/Users/${userId}/Items/${encodedItemId}`,
      { Fields: itemFields },
    )
    const item = this.mapItem(detail)
    const seriesId = detail.Type === 'Series' ? detail.Id : detail.SeriesId

    const optionalItems = async (path: string, query: Record<string, string | number | boolean | undefined>) => {
      try {
        const response = await this.request<JellyfinItemsResponse | JellyfinItemDto[]>(path, query)
        return Array.isArray(response) ? response : response.Items ?? []
      } catch {
        return []
      }
    }

    const [seasonDtos, similarDtos, specialFeatureDtos, trailerDtos] = await Promise.all([
      seriesId
        ? optionalItems(`/Shows/${encodeURIComponent(seriesId)}/Seasons`, {
            UserId: this.session.userId,
            Fields: itemFields,
          })
        : Promise.resolve([]),
      optionalItems(`/Items/${encodedItemId}/Similar`, {
        UserId: this.session.userId,
        Limit: 10,
        Fields: itemFields,
      }),
      optionalItems(`/Items/${encodedItemId}/SpecialFeatures`, {
        UserId: this.session.userId,
        Fields: itemFields,
      }),
      optionalItems(`/Items/${encodedItemId}/LocalTrailers`, {
        UserId: this.session.userId,
        Fields: itemFields,
      }),
    ])

    const seasons = seasonDtos.map(this.mapItem)
    const selectedSeason = requestedSeasonId
      ? seasons.find((season) => season.id === requestedSeasonId)
      : detail.SeasonId
        ? seasons.find((season) => season.id === detail.SeasonId)
        : seasons.find((season) => !season.watched) ?? seasons[0]
    const episodes = seriesId && selectedSeason
      ? (await optionalItems(`/Shows/${encodeURIComponent(seriesId)}/Episodes`, {
          UserId: this.session.userId,
          SeasonId: selectedSeason.id,
          Fields: itemFields,
          ImageTypeLimit: 1,
          EnableImageTypes: 'Primary,Backdrop',
        })).map(this.mapItem)
      : detail.Type === 'Episode'
        ? [item]
        : []

    return {
      item,
      seriesId,
      selectedSeasonId: selectedSeason?.id,
      seasons,
      episodes,
      similar: similarDtos.map(this.mapItem),
      extras: unique([...specialFeatureDtos, ...trailerDtos].map(this.mapItem)),
    }
  }

  async setFavorite(itemId: string, favorite: boolean) {
    await this.request<unknown>(
      `/Users/${encodeURIComponent(this.session.userId)}/FavoriteItems/${encodeURIComponent(itemId)}`,
      {},
      { method: favorite ? 'POST' : 'DELETE' },
    )
  }

  async setPlayed(itemId: string, played: boolean) {
    await this.request<unknown>(
      `/Users/${encodeURIComponent(this.session.userId)}/PlayedItems/${encodeURIComponent(itemId)}`,
      {},
      { method: played ? 'POST' : 'DELETE' },
    )
  }
}
