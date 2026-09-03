import type { MediaItem, MediaKind, MediaShelf } from './data'
import { getNativeHardwareVideoCodecs, type JellyfinSession } from './runtime'

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
  Profile?: string
  Level?: number
  BitDepth?: number
  PixelFormat?: string
  Title?: string
  DisplayTitle?: string
  Width?: number
  Height?: number
  BitRate?: number
  Channels?: number
  Language?: string
  IsDefault?: boolean
  IsForced?: boolean
  IsHearingImpaired?: boolean
  IsExternal?: boolean
  SupportsExternalStream?: boolean
  DeliveryUrl?: string
  Index?: number
}

type JellyfinMediaSource = {
  Protocol?: string
  Id?: string
  Name?: string
  Path?: string
  Container?: string
  Bitrate?: number
  RunTimeTicks?: number
  MediaStreams?: JellyfinMediaStream[]
  SupportsTranscoding?: boolean
  SupportsDirectStream?: boolean
  SupportsDirectPlay?: boolean
  DirectStreamUrl?: string
  TranscodingUrl?: string
  TranscodingContainer?: string
  TranscodingSubProtocol?: string
  DefaultAudioStreamIndex?: number
  DefaultSubtitleStreamIndex?: number
}

type JellyfinPlaybackInfoResponse = {
  MediaSources?: JellyfinMediaSource[]
  PlaySessionId?: string
  ErrorCode?: string
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

export type PlaybackTrack = {
  index: number
  label: string
  language: string
  codec: string
  channels?: number
  default: boolean
  forced: boolean
  external: boolean
  text: boolean
}

export type PlaybackEndpoint = {
  url: string
  playSessionId: string
  playMethod: 'DirectPlay' | 'DirectStream' | 'Transcode'
  transcoding: boolean
  subtitleBurnedIn: boolean
}

export type PlaybackPlan = PlaybackEndpoint & {
  itemId: string
  mediaSourceId: string
  startPositionTicks: number
  durationTicks: number
  canSeek: boolean
  container: string
  videoCodec: string
  audioCodec: string
  width?: number
  height?: number
  audioTracks: PlaybackTrack[]
  subtitleTracks: PlaybackTrack[]
  audioStreamIndex?: number
  subtitleStreamIndex: number
  subtitleUrl?: string
  fallback?: PlaybackEndpoint
}

export type PlaybackSelection = {
  mediaSourceId?: string
  audioStreamIndex?: number
  subtitleStreamIndex?: number
  forceTranscode?: boolean
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

const directPlayMaxBitrate = 120_000_000
const transcodeMaxBitrate = 24_000_000
const maximumDirectPlayWidth = 3_840
const maximumDirectPlayHeight = 2_160
const knownWebViewVideoCodecs = ['h264', 'hevc', 'vp8', 'vp9', 'av1'] as const

function browserSupportsVideoCodec(codec: string) {
  if (typeof document === 'undefined') return codec === 'h264'
  const video = document.createElement('video')
  const contentTypes: Record<string, string[]> = {
    h264: ['video/mp4; codecs="avc1.42E01E"'],
    hevc: [
      'video/mp4; codecs="hvc1.1.6.L93.B0"',
      'video/mp4; codecs="hev1.1.6.L93.B0"',
    ],
    vp8: ['video/webm; codecs="vp8"'],
    vp9: [
      'video/webm; codecs="vp09.00.31.08"',
      'video/webm; codecs="vp9"',
    ],
    av1: ['video/webm; codecs="av01.0.08M.08"'],
  }
  return (contentTypes[codec] ?? []).some((contentType) => video.canPlayType(contentType) !== '')
}

function detectHardwareVideoCodecs() {
  const nativeCodecs = getNativeHardwareVideoCodecs()
  const candidates = nativeCodecs ?? knownWebViewVideoCodecs
  return new Set(candidates.filter(browserSupportsVideoCodec))
}

function videoConditions(maximumBitDepth: number) {
  return [
    {
      Condition: 'LessThanEqual',
      Property: 'Width',
      Value: String(maximumDirectPlayWidth),
      IsRequired: false,
    },
    {
      Condition: 'LessThanEqual',
      Property: 'Height',
      Value: String(maximumDirectPlayHeight),
      IsRequired: false,
    },
    {
      Condition: 'LessThanEqual',
      Property: 'VideoBitDepth',
      Value: String(maximumBitDepth),
      IsRequired: false,
    },
  ]
}

function createWebViewDeviceProfile(hardwareVideoCodecs: ReadonlySet<string>) {
  const mp4VideoCodecs = ['h264', 'hevc'].filter((codec) => hardwareVideoCodecs.has(codec))
  const webmVideoCodecs = ['vp8', 'vp9', 'av1'].filter((codec) => hardwareVideoCodecs.has(codec))
  const eightBitVideoCodecs = ['h264', 'vp8'].filter((codec) => hardwareVideoCodecs.has(codec))
  const tenBitVideoCodecs = ['hevc', 'vp9', 'av1'].filter((codec) => hardwareVideoCodecs.has(codec))

  return {
    Name: 'Lucent Android WebView Hardware',
    MaxStreamingBitrate: directPlayMaxBitrate,
    MaxStaticBitrate: directPlayMaxBitrate,
    DirectPlayProfiles: [
      ...(mp4VideoCodecs.length > 0 ? [{
        Container: 'mp4,m4v,mov',
        Type: 'Video',
        VideoCodec: mp4VideoCodecs.join(','),
        AudioCodec: 'aac,mp3,ac3,eac3,opus',
      }] : []),
      ...(webmVideoCodecs.length > 0 ? [{
        Container: 'webm',
        Type: 'Video',
        VideoCodec: webmVideoCodecs.join(','),
        AudioCodec: 'vorbis,opus',
      }] : []),
    ],
    TranscodingProfiles: [
      {
        Container: 'ts',
        Type: 'Video',
        VideoCodec: 'h264',
        AudioCodec: 'aac,mp3',
        Protocol: 'hls',
        Context: 'Streaming',
        MaxAudioChannels: '2',
        MinSegments: 2,
        SegmentLength: 6,
        EnableSubtitlesInManifest: true,
      },
    ],
    ContainerProfiles: [],
    CodecProfiles: [
      ...(eightBitVideoCodecs.length > 0 ? [{
        Type: 'Video',
        Codec: eightBitVideoCodecs.join(','),
        Conditions: videoConditions(8),
        ApplyConditions: [],
      }] : []),
      ...(tenBitVideoCodecs.length > 0 ? [{
        Type: 'Video',
        Codec: tenBitVideoCodecs.join(','),
        Conditions: videoConditions(10),
        ApplyConditions: [],
      }] : []),
    ],
    SubtitleProfiles: [
      { Format: 'vtt', Method: 'External' },
      { Format: 'webvtt', Method: 'External' },
      { Format: 'srt', Method: 'External' },
      { Format: 'subrip', Method: 'External' },
      { Format: 'ass', Method: 'External' },
      { Format: 'ssa', Method: 'External' },
      { Format: 'mov_text', Method: 'External' },
      { Format: 'pgssub', Method: 'Encode' },
      { Format: 'dvdsub', Method: 'Encode' },
      { Format: 'dvbsub', Method: 'Encode' },
    ],
  }
}

function normalizeCodec(value: string | undefined) {
  const codec = value?.trim().toLocaleLowerCase() ?? ''
  if (['avc', 'avc1'].includes(codec)) return 'h264'
  if (['h265', 'hev1', 'hvc1'].includes(codec)) return 'hevc'
  if (codec === 'subrip') return 'srt'
  return codec
}

function normalizeContainer(value: string | undefined) {
  const container = value?.split(',')[0]?.trim().toLocaleLowerCase() ?? ''
  if (['m4v', 'mov'].includes(container)) return 'mp4'
  return container
}

function inferredVideoBitDepth(stream: JellyfinMediaStream | undefined) {
  if (stream?.BitDepth) return stream.BitDepth
  const pixelFormat = stream?.PixelFormat?.toLocaleLowerCase() ?? ''
  const match = pixelFormat.match(/(?:p|yuv\d{3}p)(\d{2})(?:le|be)?$/)
  return match ? Number(match[1]) : undefined
}

function isHardwareProfileCompatible(stream: JellyfinMediaStream | undefined) {
  if (!stream) return true
  const codec = normalizeCodec(stream.Codec)
  const profile = stream.Profile?.trim().toLocaleLowerCase() ?? ''
  const bitDepth = inferredVideoBitDepth(stream)
  const maximumBitDepth = codec === 'h264' || codec === 'vp8' ? 8 : 10
  if (bitDepth !== undefined && bitDepth > maximumBitDepth) return false

  if (codec === 'h264') {
    return !/(?:high\s*10|high\s*4:2:2|high\s*4:4:4|cavlc\s*4:4:4)/.test(profile)
  }
  if (codec === 'hevc') {
    return !/(?:main\s*12|4:2:2|4:4:4|range\s*extension|rext)/.test(profile)
  }
  if (codec === 'vp9') {
    return !/(?:profile\s*)?[13](?:\D|$)/.test(profile)
  }
  if (codec === 'av1') {
    return !/(?:high|professional)/.test(profile)
  }
  return true
}

function isWithinHardwarePlaybackLimits(stream: JellyfinMediaStream | undefined) {
  return (!stream?.Width || stream.Width <= maximumDirectPlayWidth)
    && (!stream?.Height || stream.Height <= maximumDirectPlayHeight)
    && (!stream?.BitRate || stream.BitRate <= directPlayMaxBitrate)
    && isHardwareProfileCompatible(stream)
}

function isTextSubtitle(value: string | undefined) {
  return [
    'vtt',
    'webvtt',
    'srt',
    'subrip',
    'ass',
    'ssa',
    'mov_text',
    'tx3g',
    'text',
  ].includes(normalizeCodec(value))
}

function streamsOfType(source: JellyfinMediaSource, type: 'Audio' | 'Subtitle' | 'Video') {
  return (source.MediaStreams ?? []).filter((stream) => stream.Type === type)
}

function resolveStream(
  source: JellyfinMediaSource,
  type: 'Audio' | 'Subtitle',
  requestedIndex: number | undefined,
) {
  const streams = streamsOfType(source, type)
  if (type === 'Subtitle' && requestedIndex !== undefined && requestedIndex < 0) return undefined
  const sourceDefault = type === 'Audio'
    ? source.DefaultAudioStreamIndex
    : source.DefaultSubtitleStreamIndex
  const selected = requestedIndex ?? sourceDefault
  return streams.find((stream) => stream.Index === selected)
    ?? streams.find((stream) => stream.IsDefault)
    ?? (type === 'Audio' ? streams[0] : undefined)
}

function trackLabel(stream: JellyfinMediaStream, type: 'Audio' | 'Subtitle') {
  const language = stream.Language?.trim() || (type === 'Audio' ? '未知语言' : '字幕')
  const codec = normalizeCodec(stream.Codec).toLocaleUpperCase()
  const channels = type === 'Audio' && stream.Channels ? `${stream.Channels} 声道` : ''
  const forced = stream.IsForced ? '强制' : ''
  return stream.DisplayTitle?.trim()
    || stream.Title?.trim()
    || [language, codec, channels, forced].filter(Boolean).join(' · ')
}

function mapTrack(stream: JellyfinMediaStream, type: 'Audio' | 'Subtitle'): PlaybackTrack {
  return {
    index: stream.Index ?? 0,
    label: trackLabel(stream, type),
    language: stream.Language?.trim() || '',
    codec: normalizeCodec(stream.Codec).toLocaleUpperCase(),
    channels: stream.Channels,
    default: Boolean(stream.IsDefault),
    forced: Boolean(stream.IsForced),
    external: Boolean(stream.IsExternal || stream.SupportsExternalStream),
    text: type === 'Audio' || isTextSubtitle(stream.Codec),
  }
}

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
  private readonly hardwareVideoCodecs: ReadonlySet<string>
  private readonly deviceProfile: ReturnType<typeof createWebViewDeviceProfile>

  constructor(session: JellyfinSession) {
    this.session = session
    this.hardwareVideoCodecs = detectHardwareVideoCodecs()
    this.deviceProfile = createWebViewDeviceProfile(this.hardwareVideoCodecs)
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

  private absoluteUrl(path: string) {
    if (/^https?:\/\//i.test(path)) return path
    return `${this.session.serverUrl}/${path.replace(/^\/+/, '')}`
  }

  private authenticatedUrl(path: string, query: Record<string, string | number | boolean | undefined> = {}) {
    const url = new URL(this.absoluteUrl(path))
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== '') url.searchParams.set(key, String(value))
    })
    url.searchParams.set('api_key', this.session.accessToken)
    return url.toString()
  }

  private playbackRequest(
    startPositionTicks: number,
    selection: PlaybackSelection,
    forceTranscode: boolean,
  ) {
    return {
      UserId: this.session.userId,
      StartTimeTicks: Math.max(0, Math.round(startPositionTicks)),
      MediaSourceId: selection.mediaSourceId,
      AudioStreamIndex: selection.audioStreamIndex,
      SubtitleStreamIndex: selection.subtitleStreamIndex,
      MaxStreamingBitrate: forceTranscode ? transcodeMaxBitrate : directPlayMaxBitrate,
      MaxAudioChannels: forceTranscode ? 2 : 8,
      EnableDirectPlay: !forceTranscode,
      EnableDirectStream: !forceTranscode,
      EnableTranscoding: true,
      AllowVideoStreamCopy: !forceTranscode,
      AllowAudioStreamCopy: !forceTranscode,
      AlwaysBurnInSubtitleWhenTranscoding: false,
      DeviceProfile: {
        ...this.deviceProfile,
        MaxStreamingBitrate: forceTranscode ? transcodeMaxBitrate : directPlayMaxBitrate,
        MaxStaticBitrate: forceTranscode ? transcodeMaxBitrate : directPlayMaxBitrate,
      },
    }
  }

  private directStreamUrl(
    itemId: string,
    source: JellyfinMediaSource,
    startPositionTicks: number,
    audioStreamIndex: number | undefined,
    subtitleStreamIndex: number,
    playSessionId: string,
  ) {
    if (source.DirectStreamUrl) {
      return this.authenticatedUrl(source.DirectStreamUrl)
    }

    const container = normalizeContainer(source.Container)
    const extension = container === 'webm' ? 'webm' : 'mp4'
    return this.authenticatedUrl(`/Videos/${encodeURIComponent(itemId)}/stream.${extension}`, {
      static: true,
      deviceId: this.session.deviceId,
      mediaSourceId: source.Id,
      startTimeTicks: startPositionTicks > 0 ? startPositionTicks : undefined,
      audioStreamIndex,
      subtitleStreamIndex: subtitleStreamIndex >= 0 ? subtitleStreamIndex : undefined,
      playSessionId,
    })
  }

  private subtitleUrl(
    itemId: string,
    source: JellyfinMediaSource,
    stream: JellyfinMediaStream | undefined,
  ) {
    if (!stream || stream.Index === undefined || !source.Id) return undefined
    if (stream.DeliveryUrl) return this.authenticatedUrl(stream.DeliveryUrl)
    return this.authenticatedUrl(
      `/Videos/${encodeURIComponent(itemId)}/${encodeURIComponent(source.Id)}/Subtitles/${stream.Index}/Stream.vtt`,
      {
        copyTimestamps: false,
        addVttTimeMap: false,
        startPositionTicks: 0,
      },
    )
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

  async preparePlayback(
    item: MediaItem,
    startPositionTicks = 0,
    selection: PlaybackSelection = {},
  ): Promise<PlaybackPlan> {
    if (!item.id || !item.canPlay) throw new Error('这个项目没有可播放的媒体源。')

    const startTicks = Math.max(0, Math.round(startPositionTicks))
    const path = `/Items/${encodeURIComponent(item.id)}/PlaybackInfo`
    const directResponse = await this.request<JellyfinPlaybackInfoResponse>(
      path,
      {},
      {
        method: 'POST',
        body: JSON.stringify(this.playbackRequest(startTicks, selection, false)),
      },
    )
    const sources = directResponse.MediaSources ?? []
    const source = selection.mediaSourceId
      ? sources.find((candidate) => candidate.Id === selection.mediaSourceId) ?? sources[0]
      : sources[0]
    if (!source) {
      throw new Error(directResponse.ErrorCode
        ? `Jellyfin 没有返回可播放源：${directResponse.ErrorCode}`
        : 'Jellyfin 没有返回可播放源。')
    }

    const audioTracks = streamsOfType(source, 'Audio').map((stream) => mapTrack(stream, 'Audio'))
    const subtitleTracks = streamsOfType(source, 'Subtitle').map((stream) => mapTrack(stream, 'Subtitle'))
    const selectedAudio = resolveStream(source, 'Audio', selection.audioStreamIndex)
    const selectedSubtitle = resolveStream(source, 'Subtitle', selection.subtitleStreamIndex)
    const audioStreamIndex = selectedAudio?.Index
    const subtitleStreamIndex = selectedSubtitle?.Index ?? -1
    const video = streamsOfType(source, 'Video')[0]
    const container = normalizeContainer(source.Container)
    const videoCodec = normalizeCodec(video?.Codec)
    const audioCodec = normalizeCodec(selectedAudio?.Codec)
    const browserContainer = ['mp4', 'm4v', 'mov', 'webm'].includes(container)
    const browserVideo = this.hardwareVideoCodecs.has(videoCodec)
      && isWithinHardwarePlaybackLimits(video)
    const browserAudio = !audioCodec
      || ['aac', 'mp3', 'ac3', 'eac3', 'opus', 'vorbis'].includes(audioCodec)
    const firstAudioIndex = streamsOfType(source, 'Audio')[0]?.Index
    const nonDefaultAudioSelection = selection.audioStreamIndex !== undefined
      && selection.audioStreamIndex !== firstAudioIndex
    const subtitleRequiresBurnIn = Boolean(selectedSubtitle && !isTextSubtitle(selectedSubtitle.Codec))
    const canDirectPlay = !selection.forceTranscode
      && !nonDefaultAudioSelection
      && !subtitleRequiresBurnIn
      && Boolean(source.SupportsDirectPlay)
      && browserContainer
      && browserVideo
      && browserAudio

    let transcodeResponse: JellyfinPlaybackInfoResponse | undefined
    try {
      const forcedSelection: PlaybackSelection = {
        ...selection,
        mediaSourceId: source.Id,
        audioStreamIndex,
        subtitleStreamIndex,
      }
      const transcodeRequest = this.playbackRequest(startTicks, forcedSelection, true)
      transcodeRequest.AlwaysBurnInSubtitleWhenTranscoding = subtitleRequiresBurnIn
      transcodeResponse = await this.request<JellyfinPlaybackInfoResponse>(
        path,
        {},
        { method: 'POST', body: JSON.stringify(transcodeRequest) },
      )
    } catch {
      // A direct-playable item may still work when server-side transcoding is unavailable.
    }

    const transcodedSource = transcodeResponse?.MediaSources?.find(
      (candidate) => candidate.Id === source.Id,
    ) ?? transcodeResponse?.MediaSources?.[0]
    const transcodePath = transcodedSource?.TranscodingUrl
      ?? (!canDirectPlay ? source.TranscodingUrl : undefined)
    const transcodeEndpoint: PlaybackEndpoint | undefined = transcodePath
      ? {
          url: this.authenticatedUrl(transcodePath),
          playSessionId: transcodeResponse?.PlaySessionId || directResponse.PlaySessionId || '',
          playMethod: 'Transcode',
          transcoding: true,
          subtitleBurnedIn: subtitleRequiresBurnIn,
        }
      : undefined
    const directEndpoint: PlaybackEndpoint | undefined = canDirectPlay
      ? {
          url: this.directStreamUrl(
            item.id,
            source,
            startTicks,
            audioStreamIndex,
            subtitleStreamIndex,
            directResponse.PlaySessionId ?? '',
          ),
          playSessionId: directResponse.PlaySessionId ?? '',
          playMethod: 'DirectPlay',
          transcoding: false,
          subtitleBurnedIn: false,
        }
      : undefined
    const endpoint = directEndpoint ?? transcodeEndpoint
    if (!endpoint) {
      throw new Error(transcodeResponse?.ErrorCode || directResponse.ErrorCode
        ? `当前设备与服务器没有可用的播放路径：${transcodeResponse?.ErrorCode || directResponse.ErrorCode}`
        : '当前设备与服务器没有可用的播放路径。')
    }

    return {
      ...endpoint,
      itemId: item.id,
      mediaSourceId: source.Id ?? item.id,
      startPositionTicks: startTicks,
      durationTicks: source.RunTimeTicks ?? item.runtimeTicks ?? 0,
      canSeek: true,
      container: container.toLocaleUpperCase(),
      videoCodec: videoCodec.toLocaleUpperCase(),
      audioCodec: audioCodec.toLocaleUpperCase(),
      width: video?.Width,
      height: video?.Height,
      audioTracks,
      subtitleTracks,
      audioStreamIndex,
      subtitleStreamIndex,
      subtitleUrl: !endpoint.subtitleBurnedIn && selectedSubtitle && isTextSubtitle(selectedSubtitle.Codec)
        ? this.subtitleUrl(item.id, source, selectedSubtitle)
        : undefined,
      fallback: directEndpoint ? transcodeEndpoint : undefined,
    }
  }

  async reportPlaybackStarted(plan: PlaybackPlan, paused: boolean, positionTicks: number) {
    await this.reportPlayback('/Sessions/Playing', plan, paused, positionTicks)
  }

  async reportPlaybackProgress(plan: PlaybackPlan, paused: boolean, positionTicks: number) {
    await this.reportPlayback('/Sessions/Playing/Progress', plan, paused, positionTicks)
  }

  async reportPlaybackStopped(plan: PlaybackPlan, positionTicks: number, failed = false) {
    await this.request<unknown>(
      '/Sessions/Playing/Stopped',
      {},
      {
        method: 'POST',
        body: JSON.stringify({
          ItemId: plan.itemId,
          MediaSourceId: plan.mediaSourceId,
          PositionTicks: Math.max(0, Math.round(positionTicks)),
          PlaySessionId: plan.playSessionId,
          Failed: failed,
        }),
        keepalive: true,
      },
    )
  }

  private async reportPlayback(
    path: string,
    plan: PlaybackPlan,
    paused: boolean,
    positionTicks: number,
  ) {
    await this.request<unknown>(
      path,
      {},
      {
        method: 'POST',
        body: JSON.stringify({
          CanSeek: plan.canSeek,
          ItemId: plan.itemId,
          MediaSourceId: plan.mediaSourceId,
          IsPaused: paused,
          IsMuted: false,
          PositionTicks: Math.max(0, Math.round(positionTicks)),
          PlayMethod: plan.playMethod,
          PlaySessionId: plan.playSessionId,
          AudioStreamIndex: plan.audioStreamIndex,
          SubtitleStreamIndex: plan.subtitleStreamIndex,
          RepeatMode: 'RepeatNone',
          PlaybackOrder: 'Default',
        }),
        keepalive: true,
      },
    )
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
