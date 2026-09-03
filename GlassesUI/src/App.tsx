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
  episodes,
  featured as demoFeatured,
  mediaItems as demoMediaItems,
  type MediaItem,
  type MediaShelf,
} from './data'
import { useJellyfin, type JellyfinUiStatus } from './useJellyfin'

type Page = 'home' | 'browse' | 'favorites' | 'search' | 'detail' | 'player'
type Direction = 'up' | 'down' | 'left' | 'right'

const focusableSelector = '[data-focusable="true"]:not([disabled])'
let sideNavigationReturnTarget: HTMLElement | null = null

function visibleFocusables() {
  return Array.from(document.querySelectorAll<HTMLElement>(focusableSelector)).filter((element) => {
    const rect = element.getBoundingClientRect()
    const style = window.getComputedStyle(element)
    return rect.width > 2 && rect.height > 2 && style.visibility !== 'hidden' && style.display !== 'none'
  })
}

function moveFocus(direction: Direction) {
  const nodes = visibleFocusables()
  if (!nodes.length) return

  const current = document.activeElement instanceof HTMLElement ? document.activeElement : null
  if (!current || !nodes.includes(current)) {
    const firstContent = nodes.find((node) => !node.closest('.side-navigation'))
    ;(document.querySelector<HTMLElement>('[data-autofocus="true"]') ?? firstContent ?? nodes[0]).focus()
    return
  }

  const source = current.getBoundingClientRect()
  const sx = source.left + source.width / 2
  const sy = source.top + source.height / 2
  const currentInNavigation = Boolean(current.closest('.side-navigation'))
  const navigationNodes = nodes.filter((node) => Boolean(node.closest('.side-navigation')))
  const contentNodes = nodes.filter((node) => !node.closest('.side-navigation'))

  const focusTarget = (node: HTMLElement) => {
    node.focus({ preventScroll: true })
    if (node.closest('.side-navigation')) return

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
    ;(document.querySelector<HTMLElement>('.player-play') ?? controlButtons[0])?.focus({ preventScroll: true })
    return true
  }

  if (direction === 'up' && controlIndex >= 0) {
    progress?.focus({ preventScroll: true })
    return true
  }

  if ((direction === 'left' || direction === 'right') && controlIndex >= 0) {
    const offset = direction === 'left' ? -1 : 1
    const nextIndex = Math.max(0, Math.min(controlButtons.length - 1, controlIndex + offset))
    controlButtons[nextIndex]?.focus({ preventScroll: true })
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
    next.focus({ preventScroll: true })
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

  next.focus({ preventScroll: true })
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
      firstResult.focus({ preventScroll: true })
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
  serverName,
  userName,
  refreshing,
  onNavigate,
  onPlay,
  onOpen,
  onPreview,
  onRefresh,
  onExit,
}: {
  item: MediaItem
  serverName: string
  userName: string
  refreshing: boolean
  onNavigate: (page: Page) => void
  onPlay: () => void
  onOpen: (item: MediaItem) => void
  onPreview: (item: MediaItem) => void
  onRefresh: () => void
  onExit: () => void
}) {
  const [favorite, setFavorite] = useState(Boolean(item.favorite))
  const [watched, setWatched] = useState(Boolean(item.watched))
  const [expanded, setExpanded] = useState(false)
  const [season, setSeason] = useState('第 1 季')
  const [infoTab, setInfoTab] = useState<'credits' | 'media'>('credits')
  const [detailSection, setDetailSection] = useState<'episodes' | 'similar' | 'clips' | 'details'>('episodes')
  const extraClips: MediaItem[] = [
    { ...item, id: `${item.id}-trailer`, title: '正式预告片', subtitle: '2 分 18 秒 · 4K', kind: '视频', art: item.art + 2 },
    { ...item, id: `${item.id}-making`, title: '创造深海', subtitle: '幕后制作 · 12 分钟', kind: '视频', art: item.art + 4 },
    { ...item, id: `${item.id}-cast`, title: '演员圆桌', subtitle: '特别内容 · 24 分钟', kind: '视频', art: item.art + 7 },
    { ...item, id: `${item.id}-sound`, title: '声音的形状', subtitle: '配乐特辑 · 8 分钟', kind: '视频', art: item.art + 9 },
  ]

  return (
    <div className="detail-page page-enter">
      <PageHeader active="none" minimal serverName={serverName} userName={userName} refreshing={refreshing} onNavigate={onNavigate} onRefresh={onRefresh} onExit={onExit} />
      <main className="detail-content">
        <FocusButton variant="round" className="detail-back" label="返回" onClick={() => onNavigate('home')}><ArrowLeft size={22} /></FocusButton>
        <section className="detail-hero">
          <div className="detail-poster-wrap"><ArtFrame item={item} className="detail-poster" /></div>
          <div className="detail-copy">
            <div className="detail-title-lockup">
              <div className="detail-kicker">LUCENT ORIGINAL SERIES</div>
              <h1>{item.title}</h1>
              <p className="detail-original">{item.original ?? 'A LUCENT ARCHIVE'}</p>
              <p className="detail-tagline">越向下潜，记忆越接近光。</p>
            </div>
            <div className="detail-format-badges" aria-label="媒体格式">
              <span className="detail-format-badges__rating">13+</span>
              <span>IMAX ENHANCED</span>
              <span>4K ULTRA HD</span>
              <span>◈ Dolby Vision</span>
              <span>◈ Dolby Atmos</span>
              <span>CC</span>
              <span>AD</span>
            </div>
            <div className="detail-facts">
              <span>{item.year ?? '2026'}</span><i />
              <span>共 2 季</span><i />
              <span>{item.duration ?? '52 分钟'}</span><i />
              <span>科幻、悬疑、剧情</span><i />
              <span className="detail-score"><Star size={14} fill="currentColor" /> {item.rating ?? '8.7'}</span>
            </div>
            <div className={cx('detail-overview', expanded && 'is-expanded')}>
              <p>公元 2091 年，深海测绘员林澈随“涟漪号”下潜至无人抵达的海沟。队伍在那里捕捉到一段由海水自行记录的记忆：陌生城市、倒流的雨，以及每个人未曾经历却无比熟悉的童年。</p>
              <FocusButton variant="ghost" trailing={<ChevronRight size={17} />} onClick={() => setExpanded((value) => !value)}>{expanded ? '收起剧情' : '完整剧情'}</FocusButton>
            </div>
            {item.progress !== undefined && item.progress > 0 && (
              <div className="detail-progress">
                <div><small>上次看到 S01 E03 · 潮汐记忆</small><strong>剩余约 32 分钟</strong></div>
                <span><i style={{ width: `${item.progress}%` }} /></span>
              </div>
            )}
            <div className="detail-actions">
              <FocusButton variant="primary" autoFocusTarget icon={<Play size={23} fill="currentColor" />} trailing={<span className="key-hint">ENTER</span>} onClick={onPlay}>继续 S1E3 · 潮汐记忆</FocusButton>
              <FocusButton variant="glass" icon={<RotateCcw size={20} />} onClick={onPlay}>从头播放</FocusButton>
              <FocusButton variant="round" label="播放预告片" onClick={onPlay}><MonitorPlay size={20} /></FocusButton>
              <FocusButton variant="round" active={favorite} label={favorite ? '取消收藏' : '收藏'} onClick={() => setFavorite((value) => !value)}><Heart size={20} fill={favorite ? 'currentColor' : 'none'} /></FocusButton>
              <FocusButton variant="round" active={watched} label={watched ? '标记为未看' : '标记已看'} onClick={() => setWatched((value) => !value)}><Check size={21} /></FocusButton>
              <FocusButton variant="round" label="更多操作"><MoreHorizontal size={21} /></FocusButton>
            </div>
          </div>
        </section>

        <nav className="detail-tabs" aria-label="详情内容分类">
          <FocusButton variant="ghost" active={detailSection === 'episodes'} onClick={() => setDetailSection('episodes')}>剧集</FocusButton>
          <FocusButton variant="ghost" active={detailSection === 'similar'} onClick={() => setDetailSection('similar')}>相关推荐</FocusButton>
          <FocusButton variant="ghost" active={detailSection === 'clips'} onClick={() => setDetailSection('clips')}>额外片段</FocusButton>
          <FocusButton variant="ghost" active={detailSection === 'details'} onClick={() => setDetailSection('details')}>详细信息</FocusButton>
        </nav>

        <div className="detail-tab-stage">
          {detailSection === 'episodes' && (
            <section className="episode-section detail-tab-panel">
              <header className="section-heading">
                <div><small>EPISODES</small><h2>剧集与章节</h2></div>
                <div className="season-switcher">
                  {['第 1 季', '第 2 季', '特别篇'].map((value) => <FocusButton key={value} variant="chip" active={season === value} onClick={() => setSeason(value)}>{value}</FocusButton>)}
                </div>
              </header>
              <div className="episode-rail">
                {episodes.map((episode) => {
                  const episodeItem: MediaItem = { ...item, id: `${item.id}-${episode.number}`, title: episode.title, subtitle: `第 ${episode.number} 集 · ${episode.duration}`, art: episode.art, progress: episode.progress }
                  return (
                    <button key={episode.number} type="button" data-focusable="true" className="episode-card" onClick={onPlay} onFocus={() => onPreview(episodeItem)}>
                      <ArtFrame item={episodeItem} wide />
                      <span className="episode-card__number">{episode.number}</span>
                      <span className="episode-card__play"><Play size={19} fill="currentColor" /></span>
                      <span className="episode-card__copy"><strong>{episode.title}</strong><small>{episode.duration}</small></span>
                      {episode.progress > 0 && <span className="episode-card__progress"><i style={{ width: `${episode.progress}%` }} /></span>}
                    </button>
                  )
                })}
              </div>
            </section>
          )}

          {detailSection === 'similar' && (
            <section className="similar-section detail-tab-panel">
              <header className="section-heading"><div><small>SIMILAR FREQUENCIES</small><h2>更多类似内容</h2></div></header>
              <div className="shelf__rail">
                {demoMediaItems.slice(1, 7).map((related) => <MediaCard key={related.id} item={related} wide onOpen={onOpen} onPreview={onPreview} />)}
              </div>
            </section>
          )}

          {detailSection === 'clips' && (
            <section className="similar-section detail-tab-panel">
              <header className="section-heading"><div><small>EXTRAS</small><h2>额外片段</h2></div></header>
              <div className="shelf__rail">
                {extraClips.map((clip) => <MediaCard key={clip.id} item={clip} wide onOpen={() => onPlay()} onPreview={onPreview} />)}
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
                  <dl><dt>导演</dt><dd>沈屿、周岚</dd><dt>编剧</dt><dd>季青 / Ari Chen</dd></dl>
                  <dl><dt>主演</dt><dd>林宥、顾遥、程砚、裴真</dd><dt>工作室</dt><dd>Lowlight Pictures</dd></dl>
                  <dl><dt>首播日期</dt><dd>2026 年 7 月 18 日</dd><dt>地区 / 语言</dt><dd>中国大陆 / 普通话</dd></dl>
                  <dl><dt>标签</dt><dd>深海、记忆、近未来、心理</dd><dt>路径</dt><dd>/Series/Echoes.S01/</dd></dl>
                </div>
              ) : (
                <div className="spec-grid glass-panel">
                  <div><MonitorPlay size={23} /><span><small>视频</small><strong>HEVC Main 10 · 3840×2160 · 23.976 fps</strong></span></div>
                  <div><AudioLines size={23} /><span><small>音频</small><strong>TrueHD Atmos 7.1 · 48 kHz · 中文</strong></span></div>
                  <div><Subtitles size={23} /><span><small>字幕</small><strong>ASS 中文特效 / SRT English / 关闭</strong></span></div>
                  <div><Server size={23} /><span><small>文件</small><strong>MKV · 18.6 GB · 直接播放</strong></span></div>
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

function PlayerPage({ item, onBack }: { item: MediaItem; onBack: () => void }) {
  const total = 52 * 60 + 14
  const [playing, setPlaying] = useState(true)
  const [current, setCurrent] = useState(Math.round(total * ((item.progress ?? 32) / 100)))
  const [controls, setControls] = useState(true)
  const [panel, setPanel] = useState<'audio' | 'subtitles' | null>(null)
  const [audio, setAudio] = useState('中文 · TrueHD Atmos 7.1')
  const [subtitle, setSubtitle] = useState('简体中文 · ASS 特效')
  const [feedback, setFeedback] = useState<{ direction: 'backward' | 'forward'; id: number } | null>(null)
  const hideTimer = useRef<number | null>(null)
  const feedbackTimer = useRef<number | null>(null)
  const feedbackId = useRef(0)

  const scheduleHide = useCallback(() => {
    if (hideTimer.current) window.clearTimeout(hideTimer.current)
    if (playing && !panel) {
      hideTimer.current = window.setTimeout(() => {
        document.querySelector<HTMLElement>('.player-progress__bar')?.focus({ preventScroll: true })
        setControls(false)
      }, 3200)
    }
  }, [panel, playing])

  const reveal = useCallback(() => {
    setControls(true)
    scheduleHide()
  }, [scheduleHide])

  const seek = useCallback((seconds: number, showControls = true) => {
    setCurrent((value) => Math.max(0, Math.min(total, value + seconds)))
    setFeedback({ direction: seconds > 0 ? 'forward' : 'backward', id: ++feedbackId.current })
    if (feedbackTimer.current) window.clearTimeout(feedbackTimer.current)
    feedbackTimer.current = window.setTimeout(() => setFeedback(null), 920)
    if (showControls) reveal()
  }, [reveal, total])

  useEffect(() => {
    const timer = window.setTimeout(() => {
      document.querySelector<HTMLElement>('.player-controls [data-autofocus="true"]')?.focus({ preventScroll: true })
    }, 140)
    return () => window.clearTimeout(timer)
  }, [])

  useEffect(() => {
    if (!playing) return
    const timer = window.setInterval(() => setCurrent((value) => Math.min(total, value + 1)), 1000)
    return () => window.clearInterval(timer)
  }, [playing, total])

  useEffect(() => {
    scheduleHide()
    return () => {
      if (hideTimer.current) window.clearTimeout(hideTimer.current)
      if (feedbackTimer.current) window.clearTimeout(feedbackTimer.current)
    }
  }, [scheduleHide])

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
          document.querySelector<HTMLElement>('.player-progress__bar')?.focus({ preventScroll: true })
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
            document.querySelector<HTMLElement>('.player-progress__bar')?.focus({ preventScroll: true })
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
          setPlaying((value) => !value)
          reveal()
        }
      }
    }
    window.addEventListener('lucent-player-key', listener)
    return () => window.removeEventListener('lucent-player-key', listener)
  }, [controls, reveal, seek])

  const progress = (current / total) * 100

  return (
    <div className="player-page page-enter" onMouseMove={reveal} onClick={reveal}>
      <div className="player-visual" aria-hidden="true">
        <div className="player-visual__image" />
        <div className="player-visual__caustics" />
        <div className="player-visual__vignette" />
        <div className="player-visual__grain" />
      </div>

      <div className={cx('player-chrome', !controls && 'is-hidden')}>
        <header className="player-topbar">
          <FocusButton variant="round" label="退出播放器" onClick={onBack}><ArrowLeft size={22} /></FocusButton>
          <div className="player-title"><small>正在播放 · S01 E03</small><strong>{item.title} <span>·</span> 潮汐记忆</strong></div>
          <div className="player-direct"><span /> 直接播放 <i /> 4K HEVC</div>
          <FocusButton variant="glass" icon={<AudioLines size={19} />} active={panel === 'audio'} onClick={() => { setPanel((value) => value === 'audio' ? null : 'audio'); setControls(true) }}>音轨</FocusButton>
          <FocusButton variant="glass" icon={<Captions size={19} />} active={panel === 'subtitles'} onClick={() => { setPanel((value) => value === 'subtitles' ? null : 'subtitles'); setControls(true) }}>字幕</FocusButton>
        </header>
      </div>

      <div className={cx('screen-subtitle', subtitle.startsWith('关闭') && 'is-hidden')}>当海水开始记得，我们便学会遗忘。</div>

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
              ? ['中文 · TrueHD Atmos 7.1', '中文 · AAC 2.0', 'English · EAC3 5.1']
              : ['简体中文 · ASS 特效', '繁體中文 · SRT', 'English · SRT', '关闭字幕']
            ).map((track) => {
              const selected = panel === 'audio' ? audio === track : subtitle === track
              return <FocusButton key={track} variant="glass" active={selected} trailing={selected ? <Check size={19} /> : undefined} onClick={() => panel === 'audio' ? setAudio(track) : setSubtitle(track)}>{track}</FocusButton>
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
              <FocusButton variant="round" className="player-play" autoFocusTarget label={playing ? '暂停' : '播放'} onClick={() => { setPlaying((value) => !value); reveal() }}>{playing ? <Pause size={26} fill="currentColor" /> : <Play size={26} fill="currentColor" />}</FocusButton>
              <FocusButton variant="round" label="前进十秒" onClick={() => seek(10)}><FastForward size={22} /></FocusButton>
            </div>
            <div className="player-now"><span className={cx('playing-bars', !playing && 'is-paused')}><i /><i /><i /></span><div><small>{playing ? 'NOW PLAYING' : 'PAUSED'}</small><strong>潮汐记忆</strong></div></div>
            <div className="player-control-group player-control-group--right">
              <FocusButton variant="round" label="上一集"><SkipBack size={21} /></FocusButton>
              <FocusButton variant="round" label="下一集"><SkipForward size={21} /></FocusButton>
              <FocusButton variant="round" label="音量"><Volume2 size={21} /></FocusButton>
            </div>
          </div>
          <div className="player-hints"><span><kbd>←</kbd><kbd>→</kbd> 进度焦点快退 / 快进 10 秒</span><span><kbd>↓</kbd> 显示 / 进入控制栏</span><span><kbd>ENTER</kbd> 确认</span><span><kbd>ESC</kbd> 返回详情</span></div>
        </section>
      </div>
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
  const [toast, setToast] = useState<string | null>(null)
  const toastTimer = useRef<number | null>(null)

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
    showToast('请在手机端管理 Jellyfin 登录')
  }, [showToast])

  const openItem = useCallback((item: MediaItem) => {
    setSelected(item)
    setBackdropItem(item)
    if (item.folder) navigate('browse')
    else navigate('detail')
  }, [navigate])

  useEffect(() => {
    if (page === 'player') return
    const timer = window.setTimeout(() => {
      const target = document.querySelector<HTMLElement>('[data-autofocus="true"]') ?? visibleFocusables()[0]
      target?.focus({ preventScroll: true })
    }, 180)
    return () => window.clearTimeout(timer)
  }, [page])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const key = event.key.toLowerCase()
      const target = event.target as HTMLElement | null
      if (target?.matches('input, textarea') && key !== 'escape') return

      if (key === 'escape' || key === 'backspace') {
        event.preventDefault()
        if (page === 'player') {
          goBack()
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
    if (page === 'detail') return <DetailPage key={selected.id} item={selected} serverName={serverName} userName={userName} refreshing={jellyfin.refreshing} onNavigate={(next) => next === 'home' ? goBack() : navigate(next)} onPlay={() => navigate('player')} onOpen={openItem} onPreview={setBackdropItem} onRefresh={refreshLibrary} onExit={manageLogin} />
    return <PlayerPage item={selected} onBack={goBack} />
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
