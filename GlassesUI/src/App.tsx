import {
  ArrowDownUp,
  ArrowLeft,
  AudioLines,
  Captions,
  Check,
  ChevronLeft,
  ChevronRight,
  FastForward,
  Folder,
  Grid3X3,
  Heart,
  Home,
  Info,
  Keyboard,
  Languages,
  ListFilter,
  LoaderCircle,
  LogOut,
  MonitorPlay,
  MoreHorizontal,
  Pause,
  Play,
  RefreshCw,
  Rewind,
  RotateCcw,
  Search,
  Server,
  SkipBack,
  SkipForward,
  Sparkles,
  Star,
  Subtitles,
  UserRound,
  Volume2,
  X,
} from 'lucide-react'
import Hls from 'hls.js'
import {
  type CSSProperties,
  type ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import {
  featured as demoFeatured,
  type MediaItem,
  type MediaShelf,
} from './data'
import type {
  DetailSnapshot,
  PlaybackPlan,
  PlaybackSelection,
} from './jellyfin'
import { postNativeMessage } from './runtime'
import { useJellyfin, type JellyfinUiStatus } from './useJellyfin'

type Page = 'home' | 'browse' | 'favorites' | 'search' | 'detail' | 'player'
type Direction = 'up' | 'down' | 'left' | 'right'
type PlaybackRequest = {
  item: MediaItem
  startPositionTicks: number
  key: number
}

const focusableSelector = '[data-focusable="true"]:not([disabled])'
const spatialFocusSelector = '[data-spatial-focus="true"]'
let sideNavigationReturnTarget: HTMLElement | null = null

function visibleFocusables() {
  return Array.from(document.querySelectorAll<HTMLElement>(focusableSelector)).filter((element) => {
    const rect = element.getBoundingClientRect()
    const style = window.getComputedStyle(element)
    return rect.width > 2 && rect.height > 2 && style.visibility !== 'hidden' && style.display !== 'none'
  })
}

function clearSpatialFocus(except?: HTMLElement | null) {
  document.querySelectorAll<HTMLElement>(spatialFocusSelector).forEach((element) => {
    if (element !== except) element.removeAttribute('data-spatial-focus')
  })
}

function focusSpatialElement(element?: HTMLElement | null, options: FocusOptions = { preventScroll: true }) {
  if (!element) return false
  clearSpatialFocus(element)
  element.focus(options)
  if (document.activeElement !== element) return false
  element.setAttribute('data-spatial-focus', 'true')
  return true
}

function moveFocus(direction: Direction) {
  const nodes = visibleFocusables()
  if (!nodes.length) return

  const current = document.activeElement instanceof HTMLElement ? document.activeElement : null
  if (!current || !nodes.includes(current)) {
    const firstContent = nodes.find((node) => !node.closest('.side-navigation'))
    focusSpatialElement(document.querySelector<HTMLElement>('[data-autofocus="true"]') ?? firstContent ?? nodes[0])
    return
  }

  const source = current.getBoundingClientRect()
  const sx = source.left + source.width / 2
  const sy = source.top + source.height / 2
  const currentInNavigation = Boolean(current.closest('.side-navigation'))
  const navigationNodes = nodes.filter((node) => Boolean(node.closest('.side-navigation')))
  const contentNodes = nodes.filter((node) => !node.closest('.side-navigation'))

  const focusTarget = (node: HTMLElement) => {
    focusSpatialElement(node)
    if (node.closest('.side-navigation')) return

    const playerPage = node.closest<HTMLElement>('.player-page')
    if (playerPage) {
      playerPage.scrollLeft = 0
      playerPage.scrollTop = 0

      const trackList = node.closest<HTMLElement>('.track-list')
      if (!trackList) return

      const targetRect = node.getBoundingClientRect()
      const listRect = trackList.getBoundingClientRect()
      const focusInset = 6
      const scrollDelta = targetRect.top < listRect.top + focusInset
        ? targetRect.top - listRect.top - focusInset
        : targetRect.bottom > listRect.bottom - focusInset
          ? targetRect.bottom - listRect.bottom + focusInset
          : 0

      if (scrollDelta) {
        trackList.scrollTo({
          top: trackList.scrollTop + scrollDelta,
          behavior: 'smooth',
        })
      }
      return
    }

    if (direction === 'up' && node.closest('.hero-section')) {
      window.scrollTo({ top: 0, behavior: 'smooth' })
      return
    }

    node.scrollIntoView({
      behavior: 'smooth',
      block: direction === 'up' || direction === 'down' ? 'center' : 'nearest',
      inline: 'center',
    })
  }

  if (!currentInNavigation && direction === 'up') {
    const currentShelf = current.closest('.home-page .shelf')
    const firstShelf = document.querySelector('.home-page .shelf')
    const heroPrimary = document.querySelector<HTMLElement>('.home-page .hero-actions .focus-button--primary')
    if (currentShelf && currentShelf === firstShelf && heroPrimary) {
      focusTarget(heroPrimary)
      return
    }
  }

  if (direction === 'right' && currentInNavigation && sideNavigationReturnTarget?.isConnected) {
    focusTarget(sideNavigationReturnTarget)
    sideNavigationReturnTarget = null
    return
  }

  const candidates = currentInNavigation
    ? direction === 'right' ? contentNodes : navigationNodes
    : contentNodes

  let best: { node: HTMLElement; score: number } | null = null

  for (const node of candidates) {
    if (node === current) continue
    const rect = node.getBoundingClientRect()
    const tx = rect.left + rect.width / 2
    const ty = rect.top + rect.height / 2
    const dx = tx - sx
    const dy = ty - sy
    const primary = direction === 'right' ? dx : direction === 'left' ? -dx : direction === 'down' ? dy : -dy
    if (primary <= 8) continue

    if (!currentInNavigation && (direction === 'left' || direction === 'right')) {
      const verticalGap = Math.max(0, Math.max(source.top, rect.top) - Math.min(source.bottom, rect.bottom))
      const horizontalRowTolerance = Math.max(source.height, rect.height) * .5
      if (verticalGap > horizontalRowTolerance) continue
    }

    const secondary = direction === 'left' || direction === 'right' ? Math.abs(dy) : Math.abs(dx)
    const sourceSpan = direction === 'left' || direction === 'right' ? source.height : source.width
    const targetSpan = direction === 'left' || direction === 'right' ? rect.height : rect.width
    const overlapAllowance = (sourceSpan + targetSpan) / 2
    const alignmentPenalty = secondary > overlapAllowance ? secondary * 2.4 : secondary * 0.45
    const score = primary + alignmentPenalty + Math.hypot(dx, dy) * 0.06
    if (!best || score < best.score) best = { node, score }
  }

  if (!best && direction === 'left' && !currentInNavigation) {
    sideNavigationReturnTarget = current
    const navigationTarget = document.querySelector<HTMLElement>('.side-navigation .main-nav .is-active')
      ?? document.querySelector<HTMLElement>('.side-navigation .main-nav [data-focusable="true"]')
    if (navigationTarget) focusTarget(navigationTarget)
    return
  }

  if (best) focusTarget(best.node)
}

function movePlayerFocus(direction: Direction) {
  const current = document.activeElement instanceof HTMLElement ? document.activeElement : null
  if (!current) return false

  const progress = document.querySelector<HTMLElement>('.player-progress__bar')
  const controlButtons = Array.from(document.querySelectorAll<HTMLElement>('.player-control-row [data-focusable="true"]'))
  const controlIndex = controlButtons.indexOf(current)

  if (direction === 'down' && current === progress) {
    focusSpatialElement(document.querySelector<HTMLElement>('.player-play') ?? controlButtons[0])
    return true
  }

  if (direction === 'up' && controlIndex >= 0) {
    focusSpatialElement(progress)
    return true
  }

  if ((direction === 'left' || direction === 'right') && controlIndex >= 0) {
    const offset = direction === 'left' ? -1 : 1
    const nextIndex = Math.max(0, Math.min(controlButtons.length - 1, controlIndex + offset))
    focusSpatialElement(controlButtons[nextIndex])
    return true
  }

  return false
}

function moveVirtualKeyboardFocus(current: HTMLElement, direction: Direction) {
  const keyboard = current.closest<HTMLElement>('.virtual-keyboard')
  if (!keyboard) return false

  const visibleEnabled = (element: HTMLElement) => {
    const rect = element.getBoundingClientRect()
    const style = window.getComputedStyle(element)
    return rect.width > 2 && rect.height > 2 && style.visibility !== 'hidden' && style.display !== 'none'
  }

  const collect = (selector: string) => Array.from(keyboard.querySelectorAll<HTMLElement>(selector)).filter(visibleEnabled)
  const rows = [
    collect('.keyboard-suggestions [data-focusable="true"]:not([disabled])'),
    ...Array.from(keyboard.querySelectorAll<HTMLElement>('.keyboard-row')).map((row) => (
      Array.from(row.querySelectorAll<HTMLElement>('[data-focusable="true"]:not([disabled])')).filter(visibleEnabled)
    )),
    collect('.keyboard-actions [data-focusable="true"]:not([disabled])'),
  ].filter((row) => row.length)

  const rowIndex = rows.findIndex((row) => row.includes(current))
  if (rowIndex < 0) return false

  if (direction === 'left' || direction === 'right') {
    const row = rows[rowIndex]
    const columnIndex = row.indexOf(current)
    const nextIndex = columnIndex + (direction === 'right' ? 1 : -1)
    const next = row[Math.max(0, Math.min(row.length - 1, nextIndex))]
    focusSpatialElement(next)
    return true
  }

  const nextRowIndex = rowIndex + (direction === 'down' ? 1 : -1)
  if (nextRowIndex < 0 || nextRowIndex >= rows.length) return false

  const source = current.getBoundingClientRect()
  const sourceCenter = source.left + source.width / 2
  const next = rows[nextRowIndex].reduce((closest, candidate) => {
    const rect = candidate.getBoundingClientRect()
    const distance = Math.abs(rect.left + rect.width / 2 - sourceCenter)
    return distance < closest.distance ? { element: candidate, distance } : closest
  }, { element: rows[nextRowIndex][0], distance: Number.POSITIVE_INFINITY }).element

  focusSpatialElement(next)
  next.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' })
  return true
}

function cx(...classes: Array<string | false | undefined>) {
  return classes.filter(Boolean).join(' ')
}

type FocusButtonProps = {
  children: ReactNode
  icon?: ReactNode
  trailing?: ReactNode
  variant?: 'primary' | 'ghost' | 'glass' | 'round' | 'chip' | 'danger'
  className?: string
  active?: boolean
  autoFocusTarget?: boolean
  label?: string
  disabled?: boolean
  onClick?: () => void
  onFocus?: () => void
}

function FocusButton({
  children,
  icon,
  trailing,
  variant = 'glass',
  className,
  active,
  autoFocusTarget,
  label,
  disabled,
  onClick,
  onFocus,
}: FocusButtonProps) {
  return (
    <button
      type="button"
      data-focusable="true"
      data-autofocus={autoFocusTarget ? 'true' : undefined}
      aria-label={label}
      aria-pressed={active === undefined ? undefined : active}
      disabled={disabled}
      className={cx('focus-button', `focus-button--${variant}`, active && 'is-active', className)}
      onClick={onClick}
      onFocus={onFocus}
    >
      <span className="focus-button__lens" aria-hidden="true" />
      {icon && <span className="focus-button__icon">{icon}</span>}
      <span className="focus-button__label">{children}</span>
      {trailing && <span className="focus-button__trailing">{trailing}</span>}
    </button>
  )
}

function Logo({ compact = false }: { compact?: boolean }) {
  return (
    <span className={cx('wordmark', compact && 'wordmark--compact')} aria-label="Lucent">
      <span className="wordmark__spark" />
      <span className="wordmark__name">LUCENT</span>
      {!compact && <span className="wordmark__sub">MEDIA / LIGHT</span>}
    </span>
  )
}

function AmbientBackground({ tone, imageUrl, dim = 0.45 }: { tone: number; imageUrl?: string; dim?: number }) {
  const fallbackImage = new URL(
    tone % 3 === 1 ? './assets/monochrome-flow.png' : './assets/crystal-flow.png',
    document.baseURI,
  ).href
  const style = {
    '--tone': imageUrl ? '0deg' : `${tone * 31}deg`,
    '--drift-x': `${42 + (tone % 5) * 8}%`,
    '--dim': dim,
    backgroundImage: `url(${imageUrl ?? fallbackImage})`,
  } as CSSProperties

  return (
    <div className="ambient" aria-hidden="true">
      <div className="ambient__image" style={style} />
      <div className={`ambient__spectrum ambient__spectrum--${tone % 4}`} />
      <div className="ambient__veil" />
      <div className="ambient__grain" />
    </div>
  )
}

function ArtFrame({ item, wide = false, className }: { item: MediaItem; wide?: boolean; className?: string }) {
  const fallbackImage = new URL(
    item.art % 3 === 1 ? './assets/monochrome-flow.png' : './assets/crystal-flow.png',
    document.baseURI,
  ).href
  const style = {
    '--art-hue': item.imageUrl ? '0deg' : `${item.art * 28}deg`,
    '--art-x': item.imageUrl ? '50%' : `${30 + (item.art % 5) * 14}%`,
    '--art-y': item.imageUrl ? '50%' : `${30 + (item.art % 4) * 15}%`,
    backgroundImage: `url(${item.imageUrl ?? fallbackImage})`,
  } as CSSProperties
  return (
    <div className={cx('art-frame', item.imageUrl && 'art-frame--real', wide ? 'art-frame--wide' : 'art-frame--poster', className)}>
      <div className="art-frame__image" style={style} />
      <div className={`art-frame__orb art-frame__orb--${item.art % 4}`} />
      <div className="art-frame__flare" />
      <div className="art-frame__index">L/{String(item.art + 1).padStart(2, '0')}</div>
      <div className="art-frame__title">
        <span>{item.original ?? item.title.toUpperCase()}</span>
        <strong>{item.title}</strong>
      </div>
    </div>
  )
}

type HeaderProps = {
  active: 'home' | 'browse' | 'favorites' | 'search' | 'none'
  onNavigate: (page: Page) => void
  onRefresh: () => void
  onExit: () => void
  serverName: string
  userName: string
  refreshing?: boolean
  minimal?: boolean
}

function PageHeader({ active, onNavigate, onRefresh, onExit, serverName, userName, refreshing = false, minimal = false }: HeaderProps) {
  return (
    <aside className={cx('page-header', 'side-navigation', minimal && 'side-navigation--minimal')} aria-label="全局导航">
      <span className="side-navigation__backdrop" aria-hidden="true" />
      <div className="side-navigation__inner">
        <FocusButton
          variant="ghost"
          className="logo-button side-navigation__brand"
          icon={<span className="side-navigation__brand-mark">L</span>}
          label="回到首页"
          onClick={() => onNavigate('home')}
        >
          <Logo compact />
        </FocusButton>

        <div className="side-navigation__profile" aria-label={`当前用户 ${userName}`}>
          <span className="side-navigation__avatar"><UserRound size={19} /></span>
          <span className="side-navigation__profile-copy"><small>已登录</small><strong>{userName || 'Jellyfin 用户'}</strong></span>
        </div>

        <nav className="main-nav" aria-label="主导航">
          <FocusButton className="side-navigation__item" variant="ghost" icon={<Home size={22} />} active={active === 'home'} onClick={() => onNavigate('home')}>首页</FocusButton>
          <FocusButton className="side-navigation__item" variant="ghost" icon={<Search size={22} />} active={active === 'search'} onClick={() => onNavigate('search')}>搜索</FocusButton>
          <FocusButton className="side-navigation__item" variant="ghost" icon={<Grid3X3 size={22} />} active={active === 'browse'} onClick={() => onNavigate('browse')}>媒体库</FocusButton>
          <FocusButton className="side-navigation__item" variant="ghost" icon={<Heart size={22} />} active={active === 'favorites'} onClick={() => onNavigate('favorites')}>我的收藏</FocusButton>
        </nav>

        <div className="header-spacer" />
        <div className="side-navigation__server" aria-label={`当前 Jellyfin 服务器 ${serverName}`}>
          <span className="server-pill__pulse" />
          <span><small>JELLYFIN SERVER</small><strong>{serverName || 'Jellyfin'}</strong></span>
        </div>
        <nav className="side-navigation__utilities" aria-label="服务器操作">
          <FocusButton className="side-navigation__item" variant="ghost" disabled={refreshing} icon={<RefreshCw className={cx(refreshing && 'is-spinning')} size={21} />} onClick={onRefresh}>{refreshing ? '正在刷新' : '刷新媒体库'}</FocusButton>
          <FocusButton className="side-navigation__item" variant="ghost" icon={<LogOut size={21} />} onClick={onExit}>管理登录</FocusButton>
        </nav>
      </div>
    </aside>
  )
}

function MetaRow({ item }: { item: MediaItem }) {
  const facts = [item.year, item.kind, item.duration].filter(Boolean)
  return (
    <div className="meta-row">
      {facts.map((fact, index) => <span className="meta-row__fact" key={fact}>{fact}{index < facts.length - 1 && <i />}</span>)}
      {item.rating && <span className="rating"><Star size={16} fill="currentColor" /> {item.rating}</span>}
      {item.resolution && <span className="meta-badge">{item.resolution}</span>}
    </div>
  )
}

function MediaCard({
  item,
  wide = false,
  library = false,
  onOpen,
  onPreview,
  autoFocusTarget = false,
}: {
  item: MediaItem
  wide?: boolean
  library?: boolean
  onOpen: (item: MediaItem) => void
  onPreview: (item: MediaItem) => void
  autoFocusTarget?: boolean
}) {
  return (
    <button
      type="button"
      data-focusable="true"
      data-autofocus={autoFocusTarget ? 'true' : undefined}
      className={cx('media-card', wide && 'media-card--wide', library && 'media-card--library')}
      onClick={() => onOpen(item)}
      onFocus={() => onPreview(item)}
    >
      <span className="media-card__glow" />
      <ArtFrame item={item} wide={wide || library} />
      <span className="media-card__badges">
        {item.folder && <span><Folder size={14} /> 文件夹</span>}
        {!item.folder && <span>{item.kind}</span>}
        {item.unwatched && <span className="count-badge">{item.unwatched} 未看</span>}
      </span>
      {item.favorite && <span className="media-card__favorite"><Heart size={17} fill="currentColor" /></span>}
      {item.watched && <span className="media-card__watched"><Check size={15} /> 已看</span>}
      {item.progress !== undefined && item.progress > 0 && (
        <span className="media-card__progress"><i style={{ width: `${item.progress}%` }} /></span>
      )}
      <span className="media-card__copy">
        <strong>{item.title}</strong>
        <small>{item.subtitle}</small>
      </span>
      <span className="media-card__enter"><ChevronRight size={18} /></span>
    </button>
  )
}

function HomePage({
  featured,
  shelves,
  serverName,
  userName,
  refreshing,
  onNavigate,
  onOpen,
  onPreview,
  onRefresh,
  onExit,
}: {
  featured: MediaItem
  shelves: MediaShelf[]
  serverName: string
  userName: string
  refreshing: boolean
  onNavigate: (page: Page) => void
  onOpen: (item: MediaItem) => void
  onPreview: (item: MediaItem) => void
  onRefresh: () => void
  onExit: () => void
}) {
  return (
    <div className="home-page page-enter">
      <PageHeader active="home" serverName={serverName} userName={userName} refreshing={refreshing} onNavigate={onNavigate} onRefresh={onRefresh} onExit={onExit} />
      <section className="hero-section">
        <div className="hero-section__copy">
          <div className="hero-eyebrow"><Sparkles size={17} /> LUCENT 为你推荐</div>
          <p className="hero-original">{featured.original}</p>
          <h1>{featured.title}</h1>
          {featured.tagline && <p className="hero-tagline">「{featured.tagline}」</p>}
          <MetaRow item={featured} />
          <p className="hero-overview">{featured.overview || featured.subtitle}</p>
          {featured.progress !== undefined && featured.progress > 0 && (
            <div className="hero-progress">
              <div><span>继续观看</span><strong>{featured.subtitle}</strong></div>
              <span>{featured.progress}%</span>
              <i><b style={{ width: `${featured.progress}%` }} /></i>
            </div>
          )}
          <div className="hero-actions">
            <FocusButton variant="primary" autoFocusTarget icon={featured.folder ? <Grid3X3 size={22} /> : <Play size={22} fill="currentColor" />} onClick={() => featured.folder ? onNavigate('browse') : onOpen(featured)} onFocus={() => onPreview(featured)}>{featured.folder ? '浏览媒体库' : featured.progress ? '继续观看' : '立即观看'}</FocusButton>
            <FocusButton variant="glass" icon={<Info size={21} />} onClick={() => onOpen(featured)} onFocus={() => onPreview(featured)}>查看详情</FocusButton>
          </div>
        </div>
        <div className="hero-sculpture" aria-hidden="true">
          <span className="hero-sculpture__orbit hero-sculpture__orbit--outer" />
          <span className="hero-sculpture__orbit hero-sculpture__orbit--inner" />
          <span className="hero-sculpture__core"><i /><b /></span>
          <span className="hero-sculpture__coordinate">JELLYFIN / {featured.id.slice(0, 8).toLocaleUpperCase()}</span>
        </div>
        <div className="hero-section__edition">
          <span>{featured.sourceType ?? 'MEDIA'}</span>
          <strong>{featured.indexNumber ? String(featured.indexNumber).padStart(2, '0') : '◈'}</strong>
          <small>{featured.year ?? 'LUCENT'}</small>
        </div>
        <div className="hero-section__scroll-cue"><span /> 向下探索</div>
      </section>

      <div className="shelves">
        {shelves.map((shelf, shelfIndex) => (
          <section className="shelf" key={shelf.title}>
            <header className="shelf__header">
              <div><small>{shelf.eyebrow}</small><h2>{shelf.title}</h2></div>
              <FocusButton variant="ghost" trailing={<ChevronRight size={18} />} onClick={() => onNavigate('browse')}>查看全部</FocusButton>
            </header>
            <div className="shelf__rail">
              {shelf.items.map((item, cardIndex) => (
                <MediaCard
                  key={`${shelf.id}-${item.id}`}
                  item={item}
                  library={Boolean(shelf.library)}
                  onOpen={onOpen}
                  onPreview={onPreview}
                  autoFocusTarget={shelfIndex === 0 && cardIndex === 0}
                />
              ))}
            </div>
          </section>
        ))}
      </div>
      <RemoteHint />
    </div>
  )
}

type BrowseMode = 'library' | 'favorites' | 'search'

const qwertyRows = [
  ['Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P'],
  ['A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L'],
  ['Z', 'X', 'C', 'V', 'B', 'N', 'M'],
]

const pinyinLexicon = [
  { pinyin: 'shenhai', words: ['深海', '深海回声'] },
  { pinyin: 'shen', words: ['深', '神', '身', '沈'] },
  { pinyin: 'hai', words: ['海', '海面', '海洋'] },
  { pinyin: 'kehuan', words: ['科幻', '科幻片'] },
  { pinyin: 'dianying', words: ['电影', '电影库'] },
  { pinyin: 'juji', words: ['剧集', '剧集库'] },
  { pinyin: 'jilupian', words: ['纪录片', '纪录'] },
  { pinyin: 'donghua', words: ['动画', '动画片'] },
  { pinyin: 'kecheng', words: ['课程', '网课'] },
  { pinyin: 'yinyue', words: ['音乐', '音乐会'] },
  { pinyin: 'xiju', words: ['喜剧'] },
  { pinyin: 'xuanyi', words: ['悬疑', '悬疑片'] },
  { pinyin: 'shoucang', words: ['收藏', '我的收藏'] },
  { pinyin: 'sousuo', words: ['搜索'] },
  { pinyin: 'jixu', words: ['继续', '继续观看'] },
  { pinyin: 'zuixin', words: ['最新', '最近添加'] },
  { pinyin: 'gaofen', words: ['高分', '评分最高'] },
  { pinyin: 'weizhi', words: ['未知'] },
  { pinyin: 'xinhao', words: ['信号'] },
]

function findPinyinCandidates(value: string) {
  const needle = value.trim().toLocaleLowerCase()
  if (!needle) return []

  const matches = pinyinLexicon
    .filter((entry) => entry.pinyin.startsWith(needle))
    .sort((a, b) => {
      const exactPriority = Number(b.pinyin === needle) - Number(a.pinyin === needle)
      return exactPriority || a.pinyin.localeCompare(b.pinyin, 'en-US')
    })

  return Array.from(new Set(matches.flatMap((entry) => entry.words))).slice(0, 5)
}

function BrowsePage({
  mode,
  items,
  favorites,
  searchSeed,
  serverName,
  userName,
  refreshing,
  onLoadFolder,
  onSearch,
  onNavigate,
  onOpen,
  onPreview,
  onRefresh,
  onExit,
}: {
  mode: BrowseMode
  items: MediaItem[]
  favorites: MediaItem[]
  searchSeed: MediaItem[]
  serverName: string
  userName: string
  refreshing: boolean
  onLoadFolder: (parentId: string) => Promise<MediaItem[]>
  onSearch: (query: string) => Promise<MediaItem[]>
  onNavigate: (page: Page) => void
  onOpen: (item: MediaItem) => void
  onPreview: (item: MediaItem) => void
  onRefresh: () => void
  onExit: () => void
}) {
  const [path, setPath] = useState<Array<{ item: MediaItem; children: MediaItem[] }>>([])
  const [filter, setFilter] = useState<'all' | 'unwatched' | 'continue' | 'favorite'>('all')
  const [sort, setSort] = useState<'最近加入' | '名称' | '评分最高'>('最近加入')
  const [query, setQuery] = useState('')
  const [composition, setComposition] = useState('')
  const [keyboardMode, setKeyboardMode] = useState<'zh' | 'en'>('zh')
  const [page, setPage] = useState(1)
  const [folderLoading, setFolderLoading] = useState(false)
  const [searching, setSearching] = useState(false)
  const [searchResults, setSearchResults] = useState<MediaItem[]>([])

  const keyboardRows = keyboardMode === 'zh'
    ? qwertyRows
    : [
        ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'],
        ...qwertyRows,
      ]
  const pinyinCandidates = useMemo(() => findPinyinCandidates(composition), [composition])
  const keyboardSuggestions = composition ? pinyinCandidates : ['深海', '科幻', '4K', '课程', '收藏']

  useEffect(() => {
    if (mode !== 'search') return
    if (!query.trim()) {
      setSearchResults(searchSeed.slice(0, 12))
      setSearching(false)
      return
    }

    let active = true
    setSearching(true)
    const timer = window.setTimeout(() => {
      onSearch(query).then((results) => {
        if (active) setSearchResults(results)
      }).catch(() => {
        if (active) setSearchResults([])
      }).finally(() => {
        if (active) setSearching(false)
      })
    }, 280)
    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [mode, onSearch, query, searchSeed])

  const baseItems = useMemo(() => {
    if (mode === 'favorites') return favorites
    if (mode === 'search') {
      if (!query) return searchSeed.slice(0, 12)
      return searchResults
    }
    return path.length ? path[path.length - 1].children : items
  }, [favorites, items, mode, path, query, searchResults, searchSeed])

  const shownItems = useMemo(() => {
    let result = [...baseItems]
    if (filter === 'unwatched') result = result.filter((item) => !item.watched)
    if (filter === 'continue') result = result.filter((item) => item.progress && item.progress > 0 && item.progress < 100)
    if (filter === 'favorite') result = result.filter((item) => item.favorite)
    if (sort === '名称') result.sort((a, b) => a.title.localeCompare(b.title, 'zh-CN'))
    if (sort === '评分最高') result.sort((a, b) => Number(b.rating ?? 0) - Number(a.rating ?? 0))
    return result
  }, [baseItems, filter, sort])

  const totalPages = Math.max(1, Math.ceil(shownItems.length / 12))
  const pagedItems = shownItems.slice((page - 1) * 12, page * 12)
  const title = mode === 'favorites' ? '我的收藏' : mode === 'search' ? '全局搜索' : path.at(-1)?.item.title ?? '媒体库'
  const eyebrow = mode === 'favorites' ? 'SAVED MOMENTS' : mode === 'search' ? 'SEARCH EVERYWHERE' : path.length ? 'FOLDER VIEW' : 'ALL LIBRARIES'

  useEffect(() => {
    setPage((value) => Math.min(value, totalPages))
  }, [totalPages])

  const openItem = async (item: MediaItem) => {
    if (item.folder && mode === 'library') {
      setFolderLoading(true)
      try {
        const children = await onLoadFolder(item.id)
        setPath((current) => [...current, { item, children }])
        setPage(1)
        window.scrollTo({ top: 0, behavior: 'smooth' })
      } finally {
        setFolderLoading(false)
      }
      return
    }
    onOpen(item)
  }

  const appendSearchKey = (key: string) => {
    if (keyboardMode === 'zh') {
      setComposition((value) => `${value}${key.toLocaleLowerCase()}`.slice(0, 18))
      return
    }
    setQuery((value) => `${value}${key}`.slice(0, 24))
    setFilter('all')
    setPage(1)
  }

  const commitCandidate = (term: string, replace = false) => {
    setQuery((value) => (replace ? term : `${value}${term}`).slice(0, 24))
    setComposition('')
    setFilter('all')
    setPage(1)
  }

  const eraseSearchCharacter = () => {
    if (composition) setComposition((value) => value.slice(0, -1))
    else setQuery((value) => value.slice(0, -1))
  }

  const clearSearch = () => {
    setQuery('')
    setComposition('')
    setFilter('all')
    setPage(1)
  }

  const enterSpace = () => {
    if (keyboardMode === 'zh' && composition) {
      if (pinyinCandidates[0]) commitCandidate(pinyinCandidates[0])
      return
    }
    setQuery((value) => `${value} `.slice(0, 24))
  }

  const focusFirstResult = () => {
    const firstResult = document.querySelector<HTMLElement>('.search-results .media-card')
    if (firstResult) {
      focusSpatialElement(firstResult)
      firstResult.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' })
    }
  }

  return (
    <div className={cx('browse-page', mode === 'search' && 'browse-page--search', 'page-enter')}>
      <PageHeader
        active={mode === 'favorites' ? 'favorites' : mode === 'search' ? 'search' : 'browse'}
        serverName={serverName}
        userName={userName}
        refreshing={refreshing}
        onNavigate={onNavigate}
        onRefresh={onRefresh}
        onExit={onExit}
      />
      <main className="browse-content">
        <div className="breadcrumbs">
          <FocusButton variant="round" label="返回上一级" onClick={() => path.length ? setPath((current) => current.slice(0, -1)) : onNavigate('home')}><ArrowLeft size={20} /></FocusButton>
          <FocusButton variant="ghost" onClick={() => { setPath([]); onNavigate('home') }}><Home size={16} /> 首页</FocusButton>
          {mode === 'library' && <><ChevronRight size={15} /><FocusButton variant="ghost" active={!path.length} onClick={() => setPath([])}>媒体库</FocusButton></>}
          {path.map((crumb, index) => <span className="breadcrumb-part" key={crumb.item.id}><ChevronRight size={15} /><FocusButton variant="ghost" active={index === path.length - 1} onClick={() => setPath((current) => current.slice(0, index + 1))}>{crumb.item.title}</FocusButton></span>)}
        </div>

        <header className="browse-title-row">
          <div><small>{eyebrow}</small><h1>{title}</h1><p>{mode === 'search' ? composition ? `正在输入拼音 “${composition}”` : searching ? `正在 Jellyfin 中查找 “${query}”` : query ? `正在所有媒体库中查找 “${query}”` : '使用遥控器与屏幕键盘搜索全部媒体库' : `${baseItems.length} 个项目 · Jellyfin / ${serverName}`}</p></div>
          <div className="layout-indicator"><Grid3X3 size={18} /><span>{path.length ? '横向缩略图' : '海报网格'}</span></div>
        </header>

        {mode === 'search' && (
          <div className="tv-search">
            <section className="search-console glass-panel" aria-live="polite">
              <Search size={29} />
              <div>
                <small>SEARCH ALL LIBRARIES</small>
                <strong className={cx(!query && !composition && 'is-placeholder')}>
                  {!query && !composition ? '输入拼音搜索' : <>{query}<span className="search-console__composition">{composition}</span></>}
                </strong>
              </div>
              <span className="search-console__cursor" />
              <span className="search-console__count">{composition ? '选择候选词' : searching ? '搜索中…' : query ? `${shownItems.length} 个结果` : '为你推荐'}</span>
              <FocusButton variant="round" label="删除最后一个字符" disabled={!query && !composition} onClick={eraseSearchCharacter}><ArrowLeft size={21} /></FocusButton>
            </section>

            <section className="virtual-keyboard glass-panel" aria-label="电视虚拟键盘">
              <header className="virtual-keyboard__header">
                <span><Keyboard size={19} /> {keyboardMode === 'zh' ? '拼音输入' : '英文输入'}</span>
                <div className={cx('keyboard-suggestions', composition && 'is-composing')}>
                  <small>{composition ? `${composition} · 候选` : '快捷'}</small>
                  {keyboardSuggestions.map((term, index) => (
                    <FocusButton key={term} className={cx(composition && index === 0 && 'keyboard-candidate--first')} variant="chip" active={!composition && query === term} onClick={() => commitCandidate(term, !composition)}>{term}</FocusButton>
                  ))}
                  {composition && !keyboardSuggestions.length && <span className="keyboard-suggestions__empty">暂无候选，继续输入或切换英文</span>}
                </div>
              </header>

              <div className="keyboard-rows">
                {keyboardRows.map((row, rowIndex) => (
                  <div className="keyboard-row" style={{ '--key-count': row.length, '--row-width': `${row.length * 10}%` } as CSSProperties} key={`${keyboardMode}-${rowIndex}`}>
                    {row.map((key, keyIndex) => (
                      <button
                        type="button"
                        data-focusable="true"
                        data-autofocus={rowIndex === 0 && keyIndex === 0 ? 'true' : undefined}
                        className="keyboard-key"
                        key={key}
                        aria-label={`输入 ${key}`}
                        onClick={() => appendSearchKey(key)}
                      >
                        <span>{key}</span>
                      </button>
                    ))}
                  </div>
                ))}
              </div>

              <footer className="keyboard-actions">
                <FocusButton variant="glass" icon={<Languages size={18} />} onClick={() => { setKeyboardMode((value) => value === 'zh' ? 'en' : 'zh'); setComposition('') }}>{keyboardMode === 'zh' ? 'English / 123' : '中文拼音'}</FocusButton>
                <FocusButton variant="glass" disabled={!query && !composition} onClick={clearSearch}>清空</FocusButton>
                <FocusButton variant="glass" onClick={enterSpace}>{keyboardMode === 'zh' ? '空格 / 首选词' : '空格'}</FocusButton>
                <FocusButton variant="glass" icon={<ArrowLeft size={18} />} disabled={!query && !composition} onClick={eraseSearchCharacter}>退格</FocusButton>
                <FocusButton variant="primary" icon={<Search size={19} />} disabled={!shownItems.length || !!composition} onClick={focusFirstResult}>查看 {shownItems.length} 个结果</FocusButton>
              </footer>
            </section>
          </div>
        )}

        <section className="browse-toolbar glass-panel">
          <div className="toolbar-group">
            <ListFilter size={18} /><span>筛选</span>
            {([
              ['all', '全部'],
              ['unwatched', '未观看'],
              ['continue', '可继续'],
              ['favorite', '已收藏'],
            ] as const).map(([value, label], index) => (
              <FocusButton key={value} variant="chip" active={filter === value} autoFocusTarget={mode !== 'search' && index === 0} onClick={() => { setFilter(value); setPage(1) }}>{label}</FocusButton>
            ))}
          </div>
          <span className="toolbar-divider" />
          <div className="toolbar-group toolbar-group--sort">
            <ArrowDownUp size={18} /><span>排序</span>
            {(['最近加入', '名称', '评分最高'] as const).map((value) => (
              <FocusButton key={value} variant="chip" active={sort === value} onClick={() => setSort(value)}>{value}</FocusButton>
            ))}
          </div>
        </section>

        {folderLoading ? (
          <section className="empty-state glass-panel is-loading">
            <div className="empty-state__orb"><LoaderCircle className="is-spinning" size={32} /></div>
            <small>READING LIBRARY</small>
            <h2>正在展开媒体库</h2>
            <p>从 Jellyfin 读取这个目录的内容…</p>
          </section>
        ) : shownItems.length ? (
          <section className={cx('media-grid', mode === 'search' && 'search-results', path.length > 0 && 'media-grid--wide')}>
            {pagedItems.map((item, index) => (
              <MediaCard
                key={item.id}
                item={item}
                wide={path.length > 0}
                onOpen={openItem}
                onPreview={onPreview}
                autoFocusTarget={mode !== 'search' && index === 0 && filter !== 'all'}
              />
            ))}
          </section>
        ) : (
          <section className="empty-state glass-panel">
            <div className="empty-state__orb"><Search size={32} /></div>
            <small>NOTHING IN THIS FREQUENCY</small>
            <h2>{mode === 'search' ? '没有找到这段信号' : '这里暂时空无一物'}</h2>
            <p>{mode === 'search' ? '换一个关键词，或清除筛选条件后再试。' : '当前筛选条件下没有内容，试试查看全部项目。'}</p>
            <FocusButton variant="primary" autoFocusTarget icon={<X size={19} />} onClick={() => { setQuery(''); setFilter('all') }}>清除条件</FocusButton>
          </section>
        )}

        <footer className="pagination">
          <span>第 {(page - 1) * 12 + (shownItems.length ? 1 : 0)}–{Math.min(page * 12, shownItems.length)} 项 / 共 {shownItems.length} 项</span>
          <div>
            <FocusButton variant="round" label="上一页" disabled={page === 1} onClick={() => setPage((value) => Math.max(1, value - 1))}><ChevronLeft size={21} /></FocusButton>
            <span className="pagination__page">{String(page).padStart(2, '0')} <i /> {String(totalPages).padStart(2, '0')}</span>
            <FocusButton variant="round" label="下一页" disabled={page >= totalPages} onClick={() => setPage((value) => Math.min(totalPages, value + 1))}><ChevronRight size={21} /></FocusButton>
          </div>
        </footer>
      </main>
      <RemoteHint />
    </div>
  )
}

function DetailPage({
  item,
  detail,
  loading,
  error,
  serverName,
  userName,
  refreshing,
  onNavigate,
  onPlay,
  onSelectSeason,
  onToggleFavorite,
  onToggleWatched,
  onOpen,
  onPreview,
  onRefresh,
  onExit,
}: {
  item: MediaItem
  detail: DetailSnapshot | null
  loading: boolean
  error: string
  serverName: string
  userName: string
  refreshing: boolean
  onNavigate: (page: Page) => void
  onPlay: (item: MediaItem, fromStart?: boolean) => void
  onSelectSeason: (seasonId: string) => void
  onToggleFavorite: (item: MediaItem, favorite: boolean) => Promise<boolean>
  onToggleWatched: (item: MediaItem, watched: boolean) => Promise<boolean>
  onOpen: (item: MediaItem) => void
  onPreview: (item: MediaItem) => void
  onRefresh: () => void
  onExit: () => void
}) {
  const resolvedItem = detail?.item ?? item
  const episodes = detail?.episodes ?? []
  const similar = detail?.similar ?? []
  const extras = detail?.extras ?? []
  const [favorite, setFavorite] = useState(Boolean(resolvedItem.favorite))
  const [watched, setWatched] = useState(Boolean(resolvedItem.watched))
  const [actionBusy, setActionBusy] = useState(false)
  const [expanded, setExpanded] = useState(false)
  const [infoTab, setInfoTab] = useState<'credits' | 'media'>('credits')
  const [detailSection, setDetailSection] = useState<'episodes' | 'similar' | 'clips' | 'details'>('episodes')

  useEffect(() => {
    setFavorite(Boolean(resolvedItem.favorite))
    setWatched(Boolean(resolvedItem.watched))
  }, [resolvedItem.favorite, resolvedItem.id, resolvedItem.watched])

  useEffect(() => {
    if (!detail || loading || episodes.length || detailSection !== 'episodes') return
    setDetailSection(similar.length ? 'similar' : 'details')
  }, [detail, detailSection, episodes.length, loading, similar.length])

  const playTarget = resolvedItem.canPlay
    ? resolvedItem
    : episodes.find((episode) => episode.progress && episode.progress > 0)
      ?? episodes.find((episode) => !episode.watched)
      ?? episodes[0]
  const directors = resolvedItem.people?.filter((person) => person.type === 'Director').map((person) => person.name) ?? []
  const writers = resolvedItem.people?.filter((person) => ['Writer', 'Screenplay'].includes(person.type)).map((person) => person.name) ?? []
  const actors = resolvedItem.people?.filter((person) => person.type === 'Actor').map((person) => person.name) ?? []
  const mediaItem = playTarget ?? resolvedItem
  const dimension = mediaItem.width && mediaItem.height ? `${mediaItem.width}×${mediaItem.height}` : ''
  const bitrate = mediaItem.bitrate ? `${(mediaItem.bitrate / 1_000_000).toFixed(1)} Mbps` : ''
  const premiere = resolvedItem.dateCreated
    ? new Intl.DateTimeFormat('zh-CN', { dateStyle: 'long' }).format(new Date(resolvedItem.dateCreated))
    : '未提供'

  const toggleFavorite = async () => {
    if (actionBusy) return
    setActionBusy(true)
    try {
      const next = !favorite
      if (await onToggleFavorite(resolvedItem, next)) setFavorite(next)
    } finally {
      setActionBusy(false)
    }
  }

  const toggleWatched = async () => {
    if (actionBusy) return
    setActionBusy(true)
    try {
      const next = !watched
      if (await onToggleWatched(resolvedItem, next)) setWatched(next)
    } finally {
      setActionBusy(false)
    }
  }

  return (
    <div className="detail-page page-enter">
      <PageHeader active="none" minimal serverName={serverName} userName={userName} refreshing={refreshing} onNavigate={onNavigate} onRefresh={onRefresh} onExit={onExit} />
      <main className="detail-content">
        <FocusButton variant="round" className="detail-back" label="返回" onClick={() => onNavigate('home')}><ArrowLeft size={22} /></FocusButton>
        <section className="detail-hero">
          <div className="detail-poster-wrap"><ArtFrame item={resolvedItem} className="detail-poster" /></div>
          <div className="detail-copy">
            <div className="detail-title-lockup">
              <div className="detail-kicker">JELLYFIN · {resolvedItem.sourceType?.toLocaleUpperCase() ?? 'MEDIA'}</div>
              <h1>{resolvedItem.title}</h1>
              {resolvedItem.original && <p className="detail-original">{resolvedItem.original}</p>}
              {resolvedItem.tagline && <p className="detail-tagline">{resolvedItem.tagline}</p>}
            </div>
            <div className="detail-format-badges" aria-label="媒体格式">
              {resolvedItem.officialRating && <span className="detail-format-badges__rating">{resolvedItem.officialRating}</span>}
              {mediaItem.resolution && <span>{mediaItem.resolution}</span>}
              {mediaItem.videoCodec && <span>{mediaItem.videoCodec}</span>}
              {mediaItem.audioCodec && <span>{mediaItem.audioCodec}</span>}
              {mediaItem.container && <span>{mediaItem.container}</span>}
            </div>
            <div className="detail-facts">
              {resolvedItem.year && <><span>{resolvedItem.year}</span><i /></>}
              {detail?.seasons.length ? <><span>共 {detail.seasons.length} 季</span><i /></> : null}
              {resolvedItem.duration && <><span>{resolvedItem.duration}</span><i /></>}
              {resolvedItem.genres?.length ? <><span>{resolvedItem.genres.slice(0, 4).join('、')}</span><i /></> : null}
              {resolvedItem.rating && <span className="detail-score"><Star size={14} fill="currentColor" /> {resolvedItem.rating}</span>}
            </div>
            <div className={cx('detail-overview', expanded && 'is-expanded')}>
              <p>{resolvedItem.overview || 'Jellyfin 暂未提供这项内容的剧情简介。'}</p>
              {resolvedItem.overview && resolvedItem.overview.length > 120 && <FocusButton variant="ghost" trailing={<ChevronRight size={17} />} onClick={() => setExpanded((value) => !value)}>{expanded ? '收起剧情' : '完整剧情'}</FocusButton>}
            </div>
            {playTarget?.progress !== undefined && playTarget.progress > 0 && (
              <div className="detail-progress">
                <div><small>上次看到 {playTarget.subtitle}</small><strong>已观看 {playTarget.progress}%</strong></div>
                <span><i style={{ width: `${playTarget.progress}%` }} /></span>
              </div>
            )}
            <div className="detail-actions">
              <FocusButton variant="primary" autoFocusTarget disabled={!playTarget || loading} icon={<Play size={23} fill="currentColor" />} trailing={<span className="key-hint">ENTER</span>} onClick={() => playTarget && onPlay(playTarget)}>{playTarget?.progress ? `继续 · ${playTarget.subtitle}` : '立即播放'}</FocusButton>
              <FocusButton variant="glass" disabled={!playTarget || loading} icon={<RotateCcw size={20} />} onClick={() => playTarget && onPlay(playTarget, true)}>从头播放</FocusButton>
              {extras[0] && <FocusButton variant="round" label="播放预告片" onClick={() => onPlay(extras[0], true)}><MonitorPlay size={20} /></FocusButton>}
              <FocusButton variant="round" disabled={actionBusy} active={favorite} label={favorite ? '取消收藏' : '收藏'} onClick={() => { void toggleFavorite() }}><Heart size={20} fill={favorite ? 'currentColor' : 'none'} /></FocusButton>
              <FocusButton variant="round" disabled={actionBusy} active={watched} label={watched ? '标记为未看' : '标记已看'} onClick={() => { void toggleWatched() }}><Check size={21} /></FocusButton>
              <FocusButton variant="round" label="更多操作"><MoreHorizontal size={21} /></FocusButton>
            </div>
            {loading && <div className="detail-sync"><LoaderCircle className="is-spinning" size={16} /> 正在同步详情…</div>}
            {error && <div className="detail-sync is-error">{error}</div>}
          </div>
        </section>

        <nav className="detail-tabs" aria-label="详情内容分类">
          {(episodes.length > 0 || loading) && <FocusButton variant="ghost" active={detailSection === 'episodes'} onClick={() => setDetailSection('episodes')}>剧集</FocusButton>}
          {similar.length > 0 && <FocusButton variant="ghost" active={detailSection === 'similar'} onClick={() => setDetailSection('similar')}>相关推荐</FocusButton>}
          {extras.length > 0 && <FocusButton variant="ghost" active={detailSection === 'clips'} onClick={() => setDetailSection('clips')}>额外片段</FocusButton>}
          <FocusButton variant="ghost" active={detailSection === 'details'} onClick={() => setDetailSection('details')}>详细信息</FocusButton>
        </nav>

        <div className="detail-tab-stage">
          {detailSection === 'episodes' && (
            <section className="episode-section detail-tab-panel">
              <header className="section-heading">
                <div><small>EPISODES</small><h2>剧集与章节</h2></div>
                <div className="season-switcher">
                  {detail?.seasons.map((season) => <FocusButton key={season.id} variant="chip" disabled={loading} active={detail.selectedSeasonId === season.id} onClick={() => onSelectSeason(season.id)}>{season.original || season.title}</FocusButton>)}
                </div>
              </header>
              <div className="episode-rail">
                {episodes.map((episode, index) => (
                    <button key={episode.id} type="button" data-focusable="true" className="episode-card" onClick={() => onPlay(episode)} onFocus={() => onPreview(episode)}>
                      <ArtFrame item={episode} wide />
                      <span className="episode-card__number">{String(episode.indexNumber ?? index + 1).padStart(2, '0')}</span>
                      <span className="episode-card__play"><Play size={19} fill="currentColor" /></span>
                      <span className="episode-card__copy"><strong>{episode.original || episode.title}</strong><small>{episode.duration || episode.subtitle}</small></span>
                      {episode.progress !== undefined && episode.progress > 0 && <span className="episode-card__progress"><i style={{ width: `${episode.progress}%` }} /></span>}
                    </button>
                ))}
              </div>
            </section>
          )}

          {detailSection === 'similar' && (
            <section className="similar-section detail-tab-panel">
              <header className="section-heading"><div><small>SIMILAR FREQUENCIES</small><h2>更多类似内容</h2></div></header>
              <div className="shelf__rail">
                {similar.map((related) => <MediaCard key={related.id} item={related} wide onOpen={onOpen} onPreview={onPreview} />)}
              </div>
            </section>
          )}

          {detailSection === 'clips' && (
            <section className="similar-section detail-tab-panel">
              <header className="section-heading"><div><small>EXTRAS</small><h2>额外片段</h2></div></header>
              <div className="shelf__rail">
                {extras.map((clip) => <MediaCard key={clip.id} item={clip} wide onOpen={(selectedClip) => onPlay(selectedClip, true)} onPreview={onPreview} />)}
              </div>
            </section>
          )}

          {detailSection === 'details' && (
            <section className="details-section detail-tab-panel">
              <header className="section-heading">
                <div><small>BEHIND THE FRAME</small><h2>详细信息</h2></div>
                <div className="season-switcher">
                  <FocusButton variant="chip" active={infoTab === 'credits'} onClick={() => setInfoTab('credits')}>演职与资料</FocusButton>
                  <FocusButton variant="chip" active={infoTab === 'media'} onClick={() => setInfoTab('media')}>媒体规格</FocusButton>
                </div>
              </header>
              {infoTab === 'credits' ? (
                <div className="info-grid glass-panel">
                  <dl><dt>导演</dt><dd>{directors.join('、') || '未提供'}</dd><dt>编剧</dt><dd>{writers.join('、') || '未提供'}</dd></dl>
                  <dl><dt>主演</dt><dd>{actors.slice(0, 8).join('、') || '未提供'}</dd><dt>工作室</dt><dd>{resolvedItem.studios?.join('、') || '未提供'}</dd></dl>
                  <dl><dt>加入日期</dt><dd>{premiere}</dd><dt>分类</dt><dd>{resolvedItem.kind}</dd></dl>
                  <dl><dt>标签</dt><dd>{resolvedItem.genres?.join('、') || '未提供'}</dd><dt>路径</dt><dd>{resolvedItem.path || '未提供'}</dd></dl>
                </div>
              ) : (
                <div className="spec-grid glass-panel">
                  <div><MonitorPlay size={23} /><span><small>视频</small><strong>{[mediaItem.videoCodec, dimension, mediaItem.resolution].filter(Boolean).join(' · ') || '播放时由 Jellyfin 选择规格'}</strong></span></div>
                  <div><AudioLines size={23} /><span><small>音频</small><strong>{mediaItem.audioCodec || '播放时由 Jellyfin 选择音轨'}</strong></span></div>
                  <div><Subtitles size={23} /><span><small>字幕</small><strong>播放时可选择服务器提供的字幕轨</strong></span></div>
                  <div><Server size={23} /><span><small>文件</small><strong>{[mediaItem.container, bitrate].filter(Boolean).join(' · ') || serverName}</strong></span></div>
                </div>
              )}
            </section>
          )}
        </div>
      </main>
      <RemoteHint />
    </div>
  )
}

function formatTime(totalSeconds: number) {
  const seconds = Math.max(0, Math.round(totalSeconds))
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const remainder = seconds % 60
  return `${hours ? `${hours}:` : ''}${String(minutes).padStart(hours ? 2 : 1, '0')}:${String(remainder).padStart(2, '0')}`
}

type SubtitleCue = {
  start: number
  end: number
  text: string
}

function subtitleMarkupText(source: string) {
  if (!source) return ''

  const parsed = new DOMParser().parseFromString(
    `<body>${source.replace(/<br\s*\/?>/gi, '\n')}</body>`,
    'text/html',
  )
  return (parsed.body.textContent ?? '')
    .split('\n')
    .map((line) => line.replace(/\s+/g, ' ').trim())
    .filter(Boolean)
    .join('\n')
}

function subtitleTimestamp(value: string) {
  const parts = value.replace(',', '.').split(':')
  const seconds = Number(parts.pop())
  const minutes = Number(parts.pop())
  const hours = Number(parts.pop() ?? 0)
  if (![hours, minutes, seconds].every(Number.isFinite)) return Number.NaN
  return hours * 3600 + minutes * 60 + seconds
}

function parseWebVtt(source: string) {
  const cues: SubtitleCue[] = []
  const lines = source.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n').split('\n')
  const timing = /^((?:\d+:)?\d{2}:\d{2}[.,]\d{3})\s+-->\s+((?:\d+:)?\d{2}:\d{2}[.,]\d{3})(?:\s|$)/

  for (let index = 0; index < lines.length; index += 1) {
    const match = lines[index].trim().match(timing)
    if (!match) continue

    const start = subtitleTimestamp(match[1])
    const end = subtitleTimestamp(match[2])
    const text: string[] = []
    index += 1
    while (index < lines.length && lines[index].trim()) {
      text.push(lines[index])
      index += 1
    }

    const content = subtitleMarkupText(text.join('\n'))
    if (Number.isFinite(start) && Number.isFinite(end) && end > start && content) {
      cues.push({ start, end, text: content })
    }
  }

  return cues
}

const jellyfinTicksPerSecond = 10_000_000

type PlayerStatus = 'preparing' | 'buffering' | 'playing' | 'paused' | 'ended' | 'error'

function PlayerPage({
  item,
  startPositionTicks,
  previousItem,
  nextItem,
  preparePlayback,
  reportPlaybackStarted,
  reportPlaybackProgress,
  reportPlaybackStopped,
  onPlayItem,
  onBack,
}: {
  item: MediaItem
  startPositionTicks: number
  previousItem?: MediaItem
  nextItem?: MediaItem
  preparePlayback: (item: MediaItem, positionTicks: number, selection?: PlaybackSelection) => Promise<PlaybackPlan>
  reportPlaybackStarted: (plan: PlaybackPlan, paused: boolean, positionTicks: number) => Promise<void>
  reportPlaybackProgress: (plan: PlaybackPlan, paused: boolean, positionTicks: number) => Promise<void>
  reportPlaybackStopped: (plan: PlaybackPlan, positionTicks: number, failed?: boolean) => Promise<void>
  onPlayItem: (item: MediaItem, fromStart?: boolean) => void
  onBack: () => void
}) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const hlsRef = useRef<Hls | null>(null)
  const planRef = useRef<PlaybackPlan | null>(null)
  const statusRef = useRef<PlayerStatus>('preparing')
  const prepareGeneration = useRef(0)
  const desiredPlaying = useRef(true)
  const seekAppliedKey = useRef('')
  const fallbackUsed = useRef(false)
  const startedPlans = useRef(new Set<string>())
  const stoppedPlans = useRef(new Set<string>())
  const currentRef = useRef(startPositionTicks / jellyfinTicksPerSecond)
  const [plan, setPlan] = useState<PlaybackPlan | null>(null)
  const [status, setStatus] = useState<PlayerStatus>('preparing')
  const [error, setError] = useState('')
  const [current, setCurrent] = useState(startPositionTicks / jellyfinTicksPerSecond)
  const [total, setTotal] = useState((item.runtimeTicks ?? 0) / jellyfinTicksPerSecond)
  const [controls, setControls] = useState(true)
  const [panel, setPanel] = useState<'audio' | 'subtitles' | null>(null)
  const [feedback, setFeedback] = useState<{ direction: 'backward' | 'forward'; id: number } | null>(null)
  const [volume, setVolume] = useState(100)
  const [volumeVisible, setVolumeVisible] = useState(false)
  const [subtitleCues, setSubtitleCues] = useState<SubtitleCue[]>([])
  const [subtitleLoadError, setSubtitleLoadError] = useState(false)
  const hideTimer = useRef<number | null>(null)
  const feedbackTimer = useRef<number | null>(null)
  const volumeTimer = useRef<number | null>(null)
  const feedbackId = useRef(0)
  const playing = status === 'playing' || status === 'buffering'

  const updateStatus = useCallback((nextStatus: PlayerStatus) => {
    statusRef.current = nextStatus
    setStatus(nextStatus)
  }, [])

  useEffect(() => {
    currentRef.current = current
  }, [current])

  useEffect(() => {
    const url = plan?.subtitleUrl
    setSubtitleCues([])
    setSubtitleLoadError(false)
    if (!url || (plan?.subtitleStreamIndex ?? -1) < 0) return

    const controller = new AbortController()
    void fetch(url, { signal: controller.signal })
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`)
        return response.text()
      })
      .then((source) => {
        const cues = parseWebVtt(source)
        if (!cues.length) throw new Error('WebVTT 没有可显示的字幕内容')
        setSubtitleCues(cues)
      })
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === 'AbortError') return
        setSubtitleLoadError(true)
      })

    return () => controller.abort()
  }, [plan?.playSessionId, plan?.subtitleStreamIndex, plan?.subtitleUrl])

  const planKey = useCallback((value: PlaybackPlan) => (
    `${value.itemId}:${value.playSessionId}:${value.playMethod}`
  ), [])

  const positionTicks = useCallback(() => {
    const video = videoRef.current
    const seconds = video && Number.isFinite(video.currentTime) ? video.currentTime : currentRef.current
    return Math.max(0, Math.round(seconds * jellyfinTicksPerSecond))
  }, [])

  const publishNativePlaybackState = useCallback((nextStatus: PlayerStatus | 'stopped') => {
    const active = planRef.current
    const videoDuration = videoRef.current?.duration
    const durationTicks = Number.isFinite(videoDuration) && Number(videoDuration) > 0
      ? Math.round(Number(videoDuration) * jellyfinTicksPerSecond)
      : Math.max(0, active?.durationTicks ?? item.runtimeTicks ?? 0)
    postNativeMessage({
      type: 'playback_state',
      state: nextStatus,
      itemId: item.id,
      title: item.title,
      subtitle: item.original && item.original !== item.title ? item.original : item.subtitle,
      playMethod: active?.playMethod ?? '',
      positionTicks: positionTicks(),
      durationTicks,
    })
  }, [item.id, item.original, item.runtimeTicks, item.subtitle, item.title, positionTicks])

  useEffect(() => {
    publishNativePlaybackState(status)
  }, [publishNativePlaybackState, status])

  useEffect(() => () => {
    publishNativePlaybackState('stopped')
  }, [publishNativePlaybackState])

  const stopPlan = useCallback((value: PlaybackPlan | null, failed = false) => {
    if (!value) return
    const key = planKey(value)
    if (stoppedPlans.current.has(key)) return
    stoppedPlans.current.add(key)
    void reportPlaybackStopped(value, positionTicks(), failed).catch(() => undefined)
  }, [planKey, positionTicks, reportPlaybackStopped])

  const prepare = useCallback(async (
    requestedPositionTicks: number,
    selection: PlaybackSelection = {},
    shouldPlay = true,
  ) => {
    const generation = ++prepareGeneration.current
    desiredPlaying.current = shouldPlay
    fallbackUsed.current = false
    seekAppliedKey.current = ''
    updateStatus('preparing')
    hlsRef.current?.destroy()
    hlsRef.current = null
    videoRef.current?.pause()
    setPanel(null)
    setError('')
    currentRef.current = requestedPositionTicks / jellyfinTicksPerSecond
    setCurrent(requestedPositionTicks / jellyfinTicksPerSecond)

    try {
      const next = await preparePlayback(item, requestedPositionTicks, selection)
      if (generation !== prepareGeneration.current) return
      planRef.current = next
      setPlan(next)
      setTotal((next.durationTicks || item.runtimeTicks || 0) / jellyfinTicksPerSecond)
      updateStatus('buffering')
    } catch (reason) {
      if (generation !== prepareGeneration.current) return
      planRef.current = null
      setPlan(null)
      setError(reason instanceof Error ? reason.message : '无法准备 Jellyfin 播放。')
      updateStatus('error')
      setControls(true)
    }
  }, [item, preparePlayback, updateStatus])

  useEffect(() => {
    startedPlans.current.clear()
    stoppedPlans.current.clear()
    planRef.current = null
    setPlan(null)
    void prepare(startPositionTicks)
    return () => {
      prepareGeneration.current += 1
    }
  }, [prepare, startPositionTicks])

  const failPlayback = useCallback((message: string) => {
    const active = planRef.current
    if (active?.fallback && !fallbackUsed.current) {
      fallbackUsed.current = true
      stopPlan(active, true)
      const fallback: PlaybackPlan = {
        ...active,
        ...active.fallback,
        startPositionTicks: positionTicks(),
        fallback: undefined,
      }
      seekAppliedKey.current = ''
      planRef.current = fallback
      setPlan(fallback)
      updateStatus('buffering')
      setError('')
      setControls(true)
      return
    }

    stopPlan(active, true)
    setError(message || '媒体流无法播放，请返回后重试。')
    updateStatus('error')
    setControls(true)
  }, [positionTicks, stopPlan, updateStatus])

  useEffect(() => {
    const video = videoRef.current
    if (!video || !plan) return

    video.pause()
    video.removeAttribute('src')
    video.load()
    hlsRef.current?.destroy()
    hlsRef.current = null
    seekAppliedKey.current = ''
    const hlsMedia = plan.transcoding || /\.m3u8(?:$|\?)/i.test(plan.url)

    if (hlsMedia && Hls.isSupported()) {
      let mediaRecoveryAttempted = false
      const hls = new Hls({
        enableWorker: true,
        backBufferLength: 90,
        maxBufferLength: 45,
        maxMaxBufferLength: 90,
      })
      hlsRef.current = hls
      hls.attachMedia(video)
      hls.on(Hls.Events.MEDIA_ATTACHED, () => hls.loadSource(plan.url))
      hls.on(Hls.Events.ERROR, (_event, data) => {
        if (!data.fatal) return
        if (data.type === Hls.ErrorTypes.MEDIA_ERROR && !mediaRecoveryAttempted) {
          mediaRecoveryAttempted = true
          try {
            hls.recoverMediaError()
            return
          } catch {
            // Fall through to the user-visible playback failure.
          }
        }
        failPlayback('Jellyfin HLS 媒体流已中断。')
      })
    } else {
      video.src = plan.url
      video.load()
    }

    return () => {
      hlsRef.current?.destroy()
      hlsRef.current = null
    }
  }, [failPlayback, plan])

  const applyInitialSeek = useCallback(() => {
    const video = videoRef.current
    const active = planRef.current
    if (!video || !active) return
    const key = planKey(active)
    if (seekAppliedKey.current === key) return
    seekAppliedKey.current = key
    const startSeconds = active.startPositionTicks / jellyfinTicksPerSecond
    if (startSeconds > 0 && Number.isFinite(video.duration)) {
      video.currentTime = Math.min(startSeconds, Math.max(0, video.duration - .15))
    }
    if (Number.isFinite(video.duration) && video.duration > 0) setTotal(video.duration)
    currentRef.current = video.currentTime || startSeconds
    setCurrent(video.currentTime || startSeconds)
  }, [planKey])

  const attemptPlay = useCallback(() => {
    const video = videoRef.current
    if (!video || !desiredPlaying.current) return
    applyInitialSeek()
    void video.play().catch(() => {
      desiredPlaying.current = false
      updateStatus('paused')
      setControls(true)
    })
  }, [applyInitialSeek, updateStatus])

  const togglePlayback = useCallback(() => {
    const video = videoRef.current
    if (!video) return
    if (status === 'error') {
      void prepare(positionTicks(), {
        mediaSourceId: planRef.current?.mediaSourceId,
        audioStreamIndex: planRef.current?.audioStreamIndex,
        subtitleStreamIndex: planRef.current?.subtitleStreamIndex,
      })
      return
    }
    if (video.ended) video.currentTime = 0
    if (video.paused) {
      desiredPlaying.current = true
      void video.play().catch(() => updateStatus('paused'))
    } else {
      desiredPlaying.current = false
      video.pause()
    }
  }, [positionTicks, prepare, status, updateStatus])

  const scheduleHide = useCallback(() => {
    if (hideTimer.current) window.clearTimeout(hideTimer.current)
    if (status === 'playing' && !panel) {
      hideTimer.current = window.setTimeout(() => {
        focusSpatialElement(document.querySelector<HTMLElement>('.player-progress__bar'))
        setControls(false)
      }, 3200)
    }
  }, [panel, status])

  const reveal = useCallback(() => {
    setControls(true)
    scheduleHide()
  }, [scheduleHide])

  const seek = useCallback((seconds: number, showControls = true) => {
    const video = videoRef.current
    if (!video || !Number.isFinite(video.duration) || !planRef.current?.canSeek) return
    const next = Math.max(0, Math.min(video.duration, video.currentTime + seconds))
    video.currentTime = next
    currentRef.current = next
    setCurrent(next)
    setFeedback({ direction: seconds > 0 ? 'forward' : 'backward', id: ++feedbackId.current })
    if (feedbackTimer.current) window.clearTimeout(feedbackTimer.current)
    feedbackTimer.current = window.setTimeout(() => setFeedback(null), 920)
    if (showControls) reveal()
  }, [reveal])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      focusSpatialElement(document.querySelector<HTMLElement>('.player-controls [data-autofocus="true"]'))
    }, 140)
    return () => window.clearTimeout(timer)
  }, [])

  useEffect(() => {
    scheduleHide()
    return () => {
      if (hideTimer.current) window.clearTimeout(hideTimer.current)
      if (feedbackTimer.current) window.clearTimeout(feedbackTimer.current)
      if (volumeTimer.current) window.clearTimeout(volumeTimer.current)
    }
  }, [scheduleHide])

  useEffect(() => {
    const timer = window.setInterval(() => {
      const video = videoRef.current
      const active = planRef.current
      publishNativePlaybackState(statusRef.current)
      if (!video || !active || !startedPlans.current.has(planKey(active))) return
      void reportPlaybackProgress(active, video.paused, positionTicks()).catch(() => undefined)
    }, 10_000)
    return () => window.clearInterval(timer)
  }, [planKey, positionTicks, publishNativePlaybackState, reportPlaybackProgress])

  useEffect(() => {
    const onVisibilityChange = () => {
      const active = planRef.current
      if (!active || !document.hidden || !startedPlans.current.has(planKey(active))) return
      void reportPlaybackProgress(active, true, positionTicks()).catch(() => undefined)
    }
    document.addEventListener('visibilitychange', onVisibilityChange)
    return () => document.removeEventListener('visibilitychange', onVisibilityChange)
  }, [planKey, positionTicks, reportPlaybackProgress])

  useEffect(() => {
    const onPageHide = () => stopPlan(planRef.current, statusRef.current === 'error')
    window.addEventListener('pagehide', onPageHide)
    return () => {
      window.removeEventListener('pagehide', onPageHide)
      stopPlan(planRef.current, statusRef.current === 'error')
    }
  }, [stopPlan])

  useEffect(() => {
    const listener = (event: Event) => {
      const key = (event as CustomEvent<string>).detail
      if (key === 'left' || key === 'right') {
        const active = document.activeElement
        const progressFocused = active instanceof HTMLElement
          && active.matches('.player-progress__bar')

        if (progressFocused) {
          return seek(key === 'left' ? -10 : 10, controls)
        }

        if (!controls) {
          focusSpatialElement(document.querySelector<HTMLElement>('.player-progress__bar'))
          return seek(key === 'left' ? -10 : 10, false)
        }

        reveal()
        if (!movePlayerFocus(key)) moveFocus(key)
        return
      }
      if (key === 'down') {
        const wasHidden = !controls
        reveal()
        window.setTimeout(() => {
          if (wasHidden) {
            focusSpatialElement(document.querySelector<HTMLElement>('.player-progress__bar'))
          } else {
            if (!movePlayerFocus('down')) moveFocus('down')
          }
        }, 40)
        return
      }
      if (key === 'up') {
        if (!controls) return
        reveal()
        if (!movePlayerFocus('up')) moveFocus('up')
        return
      }
      if (key === 'enter') {
        if (controls && document.activeElement instanceof HTMLElement && document.activeElement.matches(focusableSelector)) {
          document.activeElement.click()
        } else {
          togglePlayback()
          reveal()
        }
        return
      }
      if (key === 'back') {
        if (panel) {
          setPanel(null)
          reveal()
        } else {
          onBack()
        }
      }
    }
    window.addEventListener('lucent-player-key', listener)
    return () => window.removeEventListener('lucent-player-key', listener)
  }, [controls, onBack, panel, reveal, seek, togglePlayback])

  useEffect(() => {
    const listener = (event: Event) => {
      const command = String((event as CustomEvent<string>).detail ?? '')
      if (!command.startsWith('volume:')) return
      const next = Number(command.slice('volume:'.length))
      if (!Number.isFinite(next)) return
      setVolume(Math.max(0, Math.min(100, Math.round(next))))
      setVolumeVisible(true)
      if (volumeTimer.current) window.clearTimeout(volumeTimer.current)
      volumeTimer.current = window.setTimeout(() => setVolumeVisible(false), 1350)
    }
    window.addEventListener('rayneo-remote-command', listener)
    return () => window.removeEventListener('rayneo-remote-command', listener)
  }, [])

  const chooseTrack = useCallback((kind: 'audio' | 'subtitles', index: number) => {
    const active = planRef.current
    const video = videoRef.current
    if (!active || !video) return
    const shouldPlay = !video.paused
    const nextSelection: PlaybackSelection = {
      mediaSourceId: active.mediaSourceId,
      audioStreamIndex: kind === 'audio' ? index : active.audioStreamIndex,
      subtitleStreamIndex: kind === 'subtitles' ? index : active.subtitleStreamIndex,
    }
    stopPlan(active)
    void prepare(positionTicks(), nextSelection, shouldPlay)
  }, [positionTicks, prepare, stopPlan])

  const handlePlaying = useCallback(() => {
    const active = planRef.current
    updateStatus('playing')
    setError('')
    if (active) {
      const key = planKey(active)
      if (!startedPlans.current.has(key)) {
        startedPlans.current.add(key)
        void reportPlaybackStarted(active, false, positionTicks()).catch(() => undefined)
      }
    }
    scheduleHide()
  }, [planKey, positionTicks, reportPlaybackStarted, scheduleHide, updateStatus])

  const handlePause = useCallback(() => {
    const video = videoRef.current
    const active = planRef.current
    if (!video || video.ended || statusRef.current === 'preparing' || statusRef.current === 'error') return
    updateStatus('paused')
    setControls(true)
    if (active && startedPlans.current.has(planKey(active))) {
      void reportPlaybackProgress(active, true, positionTicks()).catch(() => undefined)
    }
  }, [planKey, positionTicks, reportPlaybackProgress, updateStatus])

  const handleEnded = useCallback(() => {
    desiredPlaying.current = false
    updateStatus('ended')
    setControls(true)
    stopPlan(planRef.current)
  }, [stopPlan, updateStatus])

  const progress = total > 0 ? Math.min(100, Math.max(0, current / total * 100)) : 0
  const subtitleText = useMemo(() => subtitleCues
    .filter((cue) => current >= cue.start && current < cue.end)
    .map((cue) => cue.text)
    .join('\n'), [current, subtitleCues])
  const titleDetail = item.original && item.original !== item.title ? item.original : item.subtitle
  const episodeLabel = item.sourceType === 'Episode'
    ? `S${String(item.parentIndexNumber ?? 0).padStart(2, '0')} E${String(item.indexNumber ?? 0).padStart(2, '0')}`
    : item.kind
  const playbackMethod = plan?.playMethod === 'Transcode'
    ? '服务器转码'
    : plan?.playMethod === 'DirectStream'
      ? '直接串流'
      : '直接播放'
  const formatLabel = [
    plan?.width && plan?.height ? `${plan.width}×${plan.height}` : item.resolution,
    plan?.videoCodec,
  ].filter(Boolean).join(' · ')
  const audioTracks = plan?.audioTracks ?? []
  const subtitleTracks = plan?.subtitleTracks ?? []

  return (
    <div className="player-page page-enter" onMouseMove={reveal} onClick={reveal}>
      <div className="player-visual" aria-hidden="true">
        <div className="player-visual__image" style={item.backdropUrl || item.imageUrl ? { backgroundImage: `url(${item.backdropUrl || item.imageUrl})` } : undefined} />
        <div className="player-visual__caustics" />
        <div className="player-visual__vignette" />
        <div className="player-visual__grain" />
      </div>

      <video
        ref={videoRef}
        className="player-video"
        crossOrigin="anonymous"
        playsInline
        preload="auto"
        onLoadedMetadata={applyInitialSeek}
        onCanPlay={attemptPlay}
        onPlaying={handlePlaying}
        onPause={handlePause}
        onWaiting={() => statusRef.current !== 'preparing' && updateStatus('buffering')}
        onTimeUpdate={(event) => { currentRef.current = event.currentTarget.currentTime; setCurrent(event.currentTarget.currentTime) }}
        onDurationChange={(event) => Number.isFinite(event.currentTarget.duration) && setTotal(event.currentTarget.duration)}
        onEnded={handleEnded}
        onError={() => failPlayback('浏览器无法解码当前 Jellyfin 媒体流。')}
      />

      <div className={cx('player-chrome', !controls && 'is-hidden')}>
        <header className="player-topbar">
          <FocusButton variant="round" label="退出播放器" onClick={onBack}><ArrowLeft size={22} /></FocusButton>
          <div className="player-title"><small>正在播放 · {episodeLabel}</small><strong>{item.title} <span>·</span> {titleDetail}</strong></div>
          {plan && <div className="player-direct"><span /> {playbackMethod} <i /> {formatLabel}</div>}
          <FocusButton variant="glass" disabled={!audioTracks.length || status === 'preparing'} icon={<AudioLines size={19} />} active={panel === 'audio'} onClick={() => { setPanel((value) => value === 'audio' ? null : 'audio'); setControls(true) }}>音轨</FocusButton>
          <FocusButton variant="glass" disabled={!subtitleTracks.length || status === 'preparing'} icon={<Captions size={19} />} active={panel === 'subtitles'} onClick={() => { setPanel((value) => value === 'subtitles' ? null : 'subtitles'); setControls(true) }}>字幕</FocusButton>
        </header>
      </div>

      {(status === 'preparing' || status === 'buffering') && (
        <div className="player-state" role="status">
          <LoaderCircle className="is-spinning" size={34} />
          <strong>{status === 'preparing' ? '正在准备 Jellyfin 媒体流' : '正在缓冲'}</strong>
          <small>{status === 'preparing' ? '分析设备能力、音轨与字幕' : playbackMethod}</small>
        </div>
      )}

      {status === 'error' && (
        <div className="player-error glass-panel" role="alert">
          <Info size={30} />
          <small>PLAYBACK INTERRUPTED</small>
          <h2>播放暂时中断</h2>
          <p>{error}</p>
          <div>
            <FocusButton variant="primary" autoFocusTarget icon={<RefreshCw size={18} />} onClick={() => { void prepare(positionTicks(), { mediaSourceId: plan?.mediaSourceId, audioStreamIndex: plan?.audioStreamIndex, subtitleStreamIndex: plan?.subtitleStreamIndex }) }}>重新尝试</FocusButton>
            <FocusButton variant="glass" onClick={onBack}>返回详情</FocusButton>
          </div>
        </div>
      )}

      {volumeVisible && (
        <div className="player-volume glass-panel" role="status" aria-label={`媒体音量 ${volume}%`}>
          <Volume2 size={24} />
          <span><small>媒体音量</small><strong>{volume}%</strong></span>
          <i><b style={{ width: `${volume}%` }} /></i>
        </div>
      )}

      {subtitleLoadError && (
        <div className="player-subtitle-error glass-panel" role="status">
          <Captions size={18} />
          <span>字幕加载失败，请重新选择字幕轨</span>
        </div>
      )}

      {feedback && (
        <div
          key={feedback.id}
          className={cx('seek-feedback', `seek-feedback--${feedback.direction}`)}
          role="status"
          aria-live="polite"
          aria-label={`${feedback.direction === 'forward' ? '快进' : '快退'} 10 秒`}
        >
          <div className="seek-feedback__field" aria-hidden="true"><i /><i /><i /></div>
          <div className="seek-feedback__content">
            <span className="seek-feedback__icon">
              {feedback.direction === 'forward' ? <FastForward size={42} /> : <Rewind size={42} />}
            </span>
            <span className="seek-feedback__copy">
              <small>{feedback.direction === 'forward' ? '快进' : '快退'}</small>
              <strong>10 <em>秒</em></strong>
              <b>{formatTime(current)}</b>
            </span>
          </div>
        </div>
      )}

      {panel && controls && (
        <aside className="track-panel glass-panel">
          <header><div><small>PLAYBACK OPTIONS</small><h2>{panel === 'audio' ? '选择音轨' : '选择字幕'}</h2></div><FocusButton variant="round" label="关闭面板" onClick={() => setPanel(null)}><X size={20} /></FocusButton></header>
          <div className="track-list">
            {(panel === 'audio'
              ? audioTracks
              : [{ index: -1, label: '关闭字幕', language: '', codec: '', default: false, forced: false, external: false, text: true }, ...subtitleTracks]
            ).map((track) => {
              const selected = panel === 'audio'
                ? plan?.audioStreamIndex === track.index
                : plan?.subtitleStreamIndex === track.index
              return <FocusButton key={`${panel}-${track.index}`} variant="glass" active={selected} trailing={selected ? <Check size={19} /> : undefined} onClick={() => chooseTrack(panel, track.index)}>{track.label}</FocusButton>
            })}
          </div>
        </aside>
      )}

      <div className={cx('player-chrome player-chrome--bottom', !controls && 'is-hidden')}>
        <section className="player-controls glass-panel">
          <div className="player-progress" style={{ '--played': `${progress}%` } as CSSProperties}>
            <span className="player-progress__time">{formatTime(current)}</span>
            <button type="button" data-focusable="true" aria-label="播放进度，左右键快退或快进十秒" className="player-progress__bar" onClick={() => undefined}><i><b /></i></button>
            <span className="player-progress__time">{formatTime(total)}</span>
          </div>
          <div className="player-control-row">
            <div className="player-control-group">
              <FocusButton variant="round" label="后退十秒" onClick={() => seek(-10)}><RotateCcw size={22} /></FocusButton>
              <FocusButton variant="round" disabled={status === 'preparing'} className="player-play" autoFocusTarget label={playing ? '暂停' : status === 'ended' ? '重新播放' : '播放'} onClick={() => { togglePlayback(); reveal() }}>{playing ? <Pause size={26} fill="currentColor" /> : <Play size={26} fill="currentColor" />}</FocusButton>
              <FocusButton variant="round" label="前进十秒" onClick={() => seek(10)}><FastForward size={22} /></FocusButton>
            </div>
            <div className="player-now"><span className={cx('playing-bars', !playing && 'is-paused')}><i /><i /><i /></span><div><small>{status === 'ended' ? 'PLAYBACK ENDED' : playing ? 'NOW PLAYING' : 'PAUSED'}</small><strong>{titleDetail}</strong></div></div>
            <div className="player-control-group player-control-group--right">
              <FocusButton variant="round" disabled={!previousItem} label="上一集" onClick={() => previousItem && onPlayItem(previousItem, true)}><SkipBack size={21} /></FocusButton>
              <FocusButton variant="round" disabled={!nextItem} label="下一集" onClick={() => nextItem && onPlayItem(nextItem, true)}><SkipForward size={21} /></FocusButton>
              <FocusButton variant="round" label="音量" onClick={() => { setVolumeVisible(true); if (volumeTimer.current) window.clearTimeout(volumeTimer.current); volumeTimer.current = window.setTimeout(() => setVolumeVisible(false), 1350) }}><Volume2 size={21} /></FocusButton>
            </div>
          </div>
          <div className="player-hints"><span><kbd>←</kbd><kbd>→</kbd> 进度焦点快退 / 快进 10 秒</span><span><kbd>↓</kbd> 显示 / 进入控制栏</span><span><kbd>ENTER</kbd> 确认</span><span><kbd>ESC</kbd> 返回详情</span></div>
        </section>
      </div>
      <div className={cx('screen-subtitle', !subtitleText && 'is-hidden')} aria-live="off">{subtitleText}</div>
    </div>
  )
}

function RemoteHint({ dark = false }: { dark?: boolean }) {
  return (
    <div className={cx('remote-hint', dark && 'remote-hint--dark')}>
      <span><kbd>↑</kbd><kbd>↓</kbd><kbd>←</kbd><kbd>→</kbd> / WASD 移动</span>
      <span><kbd>ENTER</kbd> 确认</span>
      <span><kbd>ESC</kbd> 返回</span>
      {import.meta.env.DEV && <span className="remote-hint__demo">1–5 页面预览</span>}
    </div>
  )
}

function RuntimeGate({
  status,
  error,
  onRetry,
}: {
  status: JellyfinUiStatus
  error: string
  onRetry: () => void
}) {
  const busy = status === 'booting' || status === 'loading'
  const title = status === 'no-session'
    ? '请在手机端登录 Jellyfin'
    : status === 'error'
      ? '媒体库连接失败'
      : '正在点亮你的媒体库'
  const description = status === 'no-session'
    ? '眼镜画面已经就绪。请使用手机端完成账号登录，媒体内容会自动出现在这里。'
    : status === 'error'
      ? error || '无法读取 Jellyfin 数据，请检查手机端登录状态与服务器网络。'
      : '正在读取媒体库、观看进度与收藏状态。'

  return (
    <div className="runtime-gate page-enter">
      <AmbientBackground tone={2} dim={0.62} />
      <header className="runtime-gate__header"><Logo /></header>
      <main className="runtime-gate__content glass-panel">
        <div className={cx('runtime-gate__orb', busy && 'is-loading')}>
          {busy ? <LoaderCircle className="is-spinning" size={38} /> : <Server size={38} />}
        </div>
        <small>{status === 'no-session' ? 'PHONE SIGN-IN REQUIRED' : busy ? 'SYNCING JELLYFIN' : 'CONNECTION INTERRUPTED'}</small>
        <h1>{title}</h1>
        <p>{description}</p>
        {!busy && status === 'error' && (
          <FocusButton variant="primary" autoFocusTarget icon={<RefreshCw size={20} />} onClick={onRetry}>重新连接</FocusButton>
        )}
        {status === 'no-session' && <div className="runtime-gate__signal"><span /> 手机端登录完成后自动刷新</div>}
      </main>
      <RemoteHint dark />
    </div>
  )
}

export default function App() {
  const jellyfin = useJellyfin()
  const [page, setPage] = useState<Page>('home')
  const [history, setHistory] = useState<Page[]>([])
  const [selected, setSelected] = useState<MediaItem>(demoFeatured)
  const [backdropItem, setBackdropItem] = useState<MediaItem>(demoFeatured)
  const [detail, setDetail] = useState<DetailSnapshot | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState('')
  const [playback, setPlayback] = useState<PlaybackRequest | null>(null)
  const [toast, setToast] = useState<string | null>(null)
  const toastTimer = useRef<number | null>(null)
  const detailGeneration = useRef(0)
  const playbackKey = useRef(0)

  const serverName = jellyfin.runtime?.session?.serverName
    || jellyfin.runtime?.session?.serverUrl.replace(/^https?:\/\//i, '')
    || 'Jellyfin'
  const userName = jellyfin.runtime?.session?.userName || 'Jellyfin 用户'

  useEffect(() => {
    const snapshot = jellyfin.snapshot
    if (!snapshot) return
    const available = [
      snapshot.featured,
      ...snapshot.libraries,
      ...snapshot.allItems,
      ...snapshot.favorites,
      ...snapshot.shelves.flatMap((shelf) => shelf.items),
    ]
    setSelected((current) => available.find((item) => item.id === current.id) ?? snapshot.featured)
    setBackdropItem((current) => available.find((item) => item.id === current.id) ?? snapshot.featured)
  }, [jellyfin.snapshot])

  useEffect(() => {
    if (page !== 'detail' || !selected.id) return
    const generation = ++detailGeneration.current
    setDetailLoading(true)
    setDetailError('')
    void jellyfin.loadDetail(selected.id).then((next) => {
      if (generation !== detailGeneration.current) return
      setDetail(next)
      setSelected(next.item)
      setBackdropItem(next.item)
    }).catch((reason) => {
      if (generation !== detailGeneration.current) return
      setDetailError(reason instanceof Error ? reason.message : '详情加载失败。')
    }).finally(() => {
      if (generation === detailGeneration.current) setDetailLoading(false)
    })
  }, [jellyfin.loadDetail, page, selected.id])

  const navigate = useCallback((next: Page) => {
    if (next === page) return
    setHistory((items) => [...items, page])
    setPage(next)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }, [page])

  const navigateDirect = useCallback((next: Page) => {
    setHistory([])
    setPage(next)
    window.scrollTo({ top: 0, behavior: 'instant' })
  }, [])

  const goBack = useCallback(() => {
    setHistory((items) => {
      if (items.length) {
        setPage(items[items.length - 1])
        return items.slice(0, -1)
      }
      setPage('home')
      return []
    })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }, [])

  const showToast = useCallback((message: string) => {
    setToast(message)
    if (toastTimer.current) window.clearTimeout(toastTimer.current)
    toastTimer.current = window.setTimeout(() => setToast(null), 2200)
  }, [])

  const refreshLibrary = useCallback(() => {
    void jellyfin.refresh().then((succeeded) => {
      showToast(succeeded ? '媒体库已刷新' : '刷新失败，请检查 Jellyfin 服务器')
    })
  }, [jellyfin.refresh, showToast])

  const manageLogin = useCallback(() => {
    postNativeMessage({ type: 'manage_login' })
    showToast('请在手机端管理 Jellyfin 登录')
  }, [showToast])

  const openItem = useCallback((item: MediaItem) => {
    setSelected(item)
    setBackdropItem(item)
    setDetail(null)
    setDetailError('')
    if (item.folder) navigate('browse')
    else navigate('detail')
  }, [navigate])

  const selectSeason = useCallback((seasonId: string) => {
    const generation = ++detailGeneration.current
    setDetailLoading(true)
    setDetailError('')
    void jellyfin.loadDetail(selected.id, seasonId).then((next) => {
      if (generation === detailGeneration.current) setDetail(next)
    }).catch((reason) => {
      if (generation === detailGeneration.current) {
        setDetailError(reason instanceof Error ? reason.message : '剧集加载失败。')
      }
    }).finally(() => {
      if (generation === detailGeneration.current) setDetailLoading(false)
    })
  }, [jellyfin.loadDetail, selected.id])

  const playItem = useCallback((item: MediaItem, fromStart = false) => {
    setPlayback({
      item,
      startPositionTicks: fromStart ? 0 : item.playbackPositionTicks ?? 0,
      key: ++playbackKey.current,
    })
    setBackdropItem(item)
    navigate('player')
  }, [navigate])

  useEffect(() => {
    const onFocusIn = (event: FocusEvent) => {
      const target = event.target instanceof HTMLElement ? event.target : null
      if (!target?.matches(spatialFocusSelector)) clearSpatialFocus()
    }
    const onPointerDown = () => clearSpatialFocus()
    const onRemoteCommand = () => {
      const active = document.activeElement
      if (!(active instanceof HTMLElement) || !active.matches(focusableSelector)) return
      clearSpatialFocus(active)
      active.setAttribute('data-spatial-focus', 'true')
    }

    document.addEventListener('focusin', onFocusIn)
    document.addEventListener('pointerdown', onPointerDown, true)
    window.addEventListener('rayneo-remote-command', onRemoteCommand)
    return () => {
      document.removeEventListener('focusin', onFocusIn)
      document.removeEventListener('pointerdown', onPointerDown, true)
      window.removeEventListener('rayneo-remote-command', onRemoteCommand)
      clearSpatialFocus()
    }
  }, [])

  useEffect(() => {
    if (page === 'player') return
    const timer = window.setTimeout(() => {
      const target = document.querySelector<HTMLElement>('[data-autofocus="true"]') ?? visibleFocusables()[0]
      focusSpatialElement(target)
    }, 180)
    return () => window.clearTimeout(timer)
  }, [jellyfin.status, page])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const key = event.key.toLowerCase()
      const target = event.target
      if (target instanceof Element && target.matches('input, textarea') && key !== 'escape') return

      if (key === 'escape' || key === 'backspace') {
        event.preventDefault()
        if (page === 'player') {
          window.dispatchEvent(new CustomEvent('lucent-player-key', { detail: 'back' }))
        } else {
          goBack()
        }
        return
      }

      if (import.meta.env.DEV && ['1', '2', '3', '4', '5'].includes(key)) {
        event.preventDefault()
        const pages: Page[] = ['home', 'browse', 'favorites', 'detail', 'player']
        navigateDirect(pages[Number(key) - 1])
        return
      }

      const directionMap: Record<string, Direction | undefined> = {
        arrowup: 'up', w: 'up', arrowdown: 'down', s: 'down', arrowleft: 'left', a: 'left', arrowright: 'right', d: 'right',
      }
      const direction = directionMap[key]

      if (page === 'player') {
        if (direction) {
          event.preventDefault()
          window.dispatchEvent(new CustomEvent('lucent-player-key', { detail: direction }))
        } else if (key === 'enter' || key === ' ') {
          event.preventDefault()
          window.dispatchEvent(new CustomEvent('lucent-player-key', { detail: 'enter' }))
        }
        return
      }

      if (direction) {
        event.preventDefault()
        const active = document.activeElement
        if (active instanceof HTMLElement && moveVirtualKeyboardFocus(active, direction)) return
        moveFocus(direction)
        return
      }

      if (key === 'enter' || key === ' ') {
        const active = document.activeElement
        if (active instanceof HTMLElement && active.matches(focusableSelector)) {
          event.preventDefault()
          active.click()
        }
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [goBack, navigateDirect, page])

  useEffect(() => () => {
    if (toastTimer.current) window.clearTimeout(toastTimer.current)
  }, [])

  if (jellyfin.status !== 'ready' || !jellyfin.snapshot) {
    return (
      <RuntimeGate
        status={jellyfin.status}
        error={jellyfin.error}
        onRetry={() => { void jellyfin.retry() }}
      />
    )
  }

  const snapshot = jellyfin.snapshot

  const pageNode = (() => {
    if (page === 'home') return <HomePage featured={snapshot.featured} shelves={snapshot.shelves} serverName={serverName} userName={userName} refreshing={jellyfin.refreshing} onNavigate={navigate} onOpen={openItem} onPreview={setBackdropItem} onRefresh={refreshLibrary} onExit={manageLogin} />
    if (page === 'browse' || page === 'favorites' || page === 'search') {
      return <BrowsePage key={page} mode={page === 'browse' ? 'library' : page} items={snapshot.libraries} favorites={snapshot.favorites} searchSeed={snapshot.allItems} serverName={serverName} userName={userName} refreshing={jellyfin.refreshing} onLoadFolder={jellyfin.loadFolder} onSearch={jellyfin.search} onNavigate={navigate} onOpen={openItem} onPreview={setBackdropItem} onRefresh={refreshLibrary} onExit={manageLogin} />
    }
    if (page === 'detail') return <DetailPage key={selected.id} item={selected} detail={detail} loading={detailLoading} error={detailError} serverName={serverName} userName={userName} refreshing={jellyfin.refreshing} onNavigate={(next) => next === 'home' ? goBack() : navigate(next)} onPlay={playItem} onSelectSeason={selectSeason} onToggleFavorite={async (target, favorite) => { try { const saved = await jellyfin.setFavorite(target, favorite); if (saved) showToast(favorite ? '已加入收藏' : '已取消收藏'); return saved } catch { showToast('收藏状态更新失败'); return false } }} onToggleWatched={async (target, watched) => { try { const saved = await jellyfin.setPlayed(target, watched); if (saved) showToast(watched ? '已标记为看过' : '已标记为未看'); return saved } catch { showToast('观看状态更新失败'); return false } }} onOpen={openItem} onPreview={setBackdropItem} onRefresh={refreshLibrary} onExit={manageLogin} />
    const request = playback ?? {
      item: selected.canPlay
        ? selected
        : snapshot.allItems.find((item) => item.canPlay) ?? snapshot.featured,
      startPositionTicks: selected.playbackPositionTicks ?? 0,
      key: 0,
    }
    const episodeIndex = detail?.episodes.findIndex((episode) => episode.id === request.item.id) ?? -1
    return <PlayerPage key={request.key} item={request.item} startPositionTicks={request.startPositionTicks} previousItem={episodeIndex > 0 ? detail?.episodes[episodeIndex - 1] : undefined} nextItem={episodeIndex >= 0 ? detail?.episodes[episodeIndex + 1] : undefined} preparePlayback={jellyfin.preparePlayback} reportPlaybackStarted={jellyfin.reportPlaybackStarted} reportPlaybackProgress={jellyfin.reportPlaybackProgress} reportPlaybackStopped={jellyfin.reportPlaybackStopped} onPlayItem={playItem} onBack={goBack} />
  })()

  return (
    <div className={cx('app', `app--${page}`)}>
      {page !== 'player' && <AmbientBackground tone={backdropItem.art} imageUrl={backdropItem.backdropUrl} dim={page === 'home' ? 0.72 : page === 'detail' ? 0.48 : 0.42} />}
      {pageNode}
      {toast && <div className="toast"><span><Check size={18} /></span>{toast}</div>}
      <svg className="svg-filters" aria-hidden="true">
        <filter id="liquid-edge" x="-30%" y="-30%" width="160%" height="160%">
          <feTurbulence type="fractalNoise" baseFrequency="0.012 0.06" numOctaves="2" seed="8" result="noise" />
          <feDisplacementMap in="SourceGraphic" in2="noise" scale="5" xChannelSelector="R" yChannelSelector="B" />
        </filter>
      </svg>
    </div>
  )
}
