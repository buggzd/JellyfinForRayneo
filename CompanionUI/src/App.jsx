import React, { useEffect, useMemo, useRef, useState } from 'react'
import {
  ArrowLeft,
  ArrowRight,
  Box,
  Check,
  ChevronRight,
  CircleHelp,
  Copy,
  ExternalLink,
  Eye,
  EyeOff,
  Glasses,
  KeyRound,
  Link2,
  LockKeyhole,
  Monitor,
  MoreHorizontal,
  Plus,
  Radar,
  Radio,
  RefreshCw,
  RotateCcw,
  Router,
  Server,
  Settings2,
  Share2,
  ShieldCheck,
  SlidersHorizontal,
  Sparkles,
  UserRound,
  Vibrate,
  Wifi,
  X,
  Zap,
} from 'lucide-react'

const DEMO_SERVERS = [
  {
    id: 'jellyfin-home',
    name: '家庭媒体库',
    host: 'jellyfin.local:8096',
    detail: 'Jellyfin 10.10',
    latency: '8 ms',
    strength: 3,
  },
  {
    id: 'media-nas',
    name: 'Media NAS',
    host: 'media.local:8096',
    detail: 'Jellyfin 10.10',
    latency: '21 ms',
    strength: 2,
  },
]

const springEase = 'cubic-bezier(.2, .9, .25, 1.25)'
const assetUrl = (name) => `${import.meta.env.BASE_URL}art/${name}`

function hasNativeBridge() {
  return typeof window !== 'undefined' && typeof window.JellyfinNative === 'object'
}

function callNative(method, ...args) {
  if (!hasNativeBridge() || typeof window.JellyfinNative[method] !== 'function') return undefined
  try {
    return window.JellyfinNative[method](...args)
  } catch {
    return undefined
  }
}

function parseNativePayload(payload) {
  if (!payload) return null
  if (typeof payload === 'object') return payload
  try {
    return JSON.parse(payload)
  } catch {
    return null
  }
}

function serverFromNative(state) {
  if (!state?.serverUrl) return null
  return {
    id: state.serverId || state.serverUrl,
    name: state.serverName || 'Jellyfin 媒体库',
    host: state.serverUrl,
    detail: state.serverVersion ? `Jellyfin ${state.serverVersion}` : 'Jellyfin 服务器',
    latency: '已保存',
    strength: 3,
  }
}

function profileInitials(username) {
  const normalized = (username || 'Jellyfin').trim()
  if (!normalized) return 'JF'
  return normalized.slice(0, 2).toUpperCase()
}

function formatQuickCode(value) {
  const compact = (value || '').replace(/[^a-zA-Z0-9]/g, '').toUpperCase()
  if (compact.length <= 3) return compact
  return `${compact.slice(0, 3)} · ${compact.slice(3)}`
}

function formatPlaybackTime(ticks) {
  const seconds = Math.max(0, Math.floor(Number(ticks || 0) / 10_000_000))
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const remaining = seconds % 60
  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remaining).padStart(2, '0')}`
    : `${minutes}:${String(remaining).padStart(2, '0')}`
}

function useStoredState(key, initialValue) {
  const [value, setValue] = useState(() => {
    try {
      const stored = localStorage.getItem(key)
      return stored ? JSON.parse(stored) : initialValue
    } catch {
      return initialValue
    }
  })

  useEffect(() => {
    localStorage.setItem(key, JSON.stringify(value))
  }, [key, value])

  return [value, setValue]
}

function App() {
  const isNative = useMemo(() => hasNativeBridge(), [])
  const [session, setSession] = useStoredState('jellyfin-rayneo-session', null)
  const [displayMode, setDisplayMode] = useStoredState('jellyfin-rayneo-display', 'stereo')
  const [haptics, setHaptics] = useStoredState('jellyfin-rayneo-haptics', true)
  const [screen, setScreen] = useState(() => (!isNative && session ? 'home' : 'connect'))
  const [selectedServer, setSelectedServer] = useState(DEMO_SERVERS[0])
  const [servers, setServers] = useState(() => (isNative ? [] : DEMO_SERVERS))
  const [authMode, setAuthMode] = useState('password')
  const [manualOpen, setManualOpen] = useState(false)
  const [toast, setToast] = useState('')
  const [nativeState, setNativeState] = useState(null)
  const toastTimer = useRef(null)
  const screenRef = useRef(screen)
  const touchpadReadyRef = useRef(false)
  const lastNativeErrorRef = useRef('')
  const lastNativePayloadRef = useRef('')
  const opticsFrameRef = useRef(0)
  const opticsSampleRef = useRef(null)
  const opticsButtonRef = useRef(null)
  const opticsRectRef = useRef(null)

  const notify = (message) => {
    window.clearTimeout(toastTimer.current)
    setToast(message)
    toastTimer.current = window.setTimeout(() => setToast(''), 2100)
  }

  const go = (next) => {
    window.scrollTo({ top: 0, behavior: 'smooth' })
    if (next === 'touchpad') setToast('')
    screenRef.current = next
    setScreen(next)
  }

  useEffect(() => {
    screenRef.current = screen
    if (isNative) callNative('screenChanged', screen)
  }, [isNative, screen])

  useEffect(() => {
    const invalidateOpticsRect = () => {
      opticsRectRef.current = null
    }

    window.addEventListener('scroll', invalidateOpticsRect, true)
    window.addEventListener('resize', invalidateOpticsRect)
    return () => {
      window.removeEventListener('scroll', invalidateOpticsRect, true)
      window.removeEventListener('resize', invalidateOpticsRect)
      if (opticsFrameRef.current) {
        window.cancelAnimationFrame(opticsFrameRef.current)
        opticsFrameRef.current = 0
      }
    }
  }, [])

  useEffect(() => {
    if (!isNative) return undefined

    const receiveState = (payload) => {
      const next = parseNativePayload(payload)
      if (!next) return
      const signature = typeof payload === 'string' ? payload : JSON.stringify(next)
      if (signature === lastNativePayloadRef.current) return
      lastNativePayloadRef.current = signature

      setNativeState(next)
      setDisplayMode(next.displayMode === 'stereo_screen' ? 'stereo' : 'mirror')
      setServers(Array.isArray(next.servers) ? next.servers : [])

      const stateServer = serverFromNative(next)
      if (stateServer) setSelectedServer(stateServer)

      if (next.sessionAvailable) {
        const activeServer = stateServer || selectedServer
        setSession({
          username: next.username || 'Jellyfin',
          server: activeServer,
          restored: true,
          saved: Boolean(next.sessionSaved),
        })
        if (screenRef.current === 'connect' || screenRef.current === 'auth') go('home')
      } else {
        setSession(null)
        if (screenRef.current === 'home' || screenRef.current === 'settings') go('connect')
      }

      if (next.state === 'quick_connect_waiting') {
        setAuthMode('quick')
        if (screenRef.current !== 'touchpad') go('auth')
      }

      if (next.isError && next.message && next.message !== lastNativeErrorRef.current) {
        lastNativeErrorRef.current = next.message
        notify(next.message)
      } else if (!next.isError) {
        lastNativeErrorRef.current = ''
      }

      const touchpadBecameReady = Boolean(next.touchpadReady) && !touchpadReadyRef.current
      touchpadReadyRef.current = Boolean(next.touchpadReady)
      if (touchpadBecameReady && (screenRef.current === 'home' || screenRef.current === 'settings')) {
        go('touchpad')
      }
    }

    const nativeApi = {
      receiveState,
      openScreen: (requestedScreen) => {
        if (requestedScreen === 'settings') {
          go('settings')
          return
        }
        setAuthMode('password')
        go('connect')
      },
      handleBack: () => {
        if (screenRef.current === 'touchpad' || screenRef.current === 'settings') {
          go('home')
        } else if (screenRef.current === 'auth') {
          callNative('cancelQuickConnect')
          setAuthMode('password')
          go('connect')
        }
      },
    }
    window.LumaNative = nativeApi

    receiveState(callNative('getState'))
    callNative('ready')

    return () => {
      if (window.LumaNative === nativeApi) delete window.LumaNative
    }
  }, [isNative])

  const chooseServer = (server) => {
    setSelectedServer(server)
    if (isNative) callNative('selectServer', server.host, server.name)
    setAuthMode('password')
    go('auth')
  }

  const finishLogin = (username = 'demo') => {
    const nextSession = {
      username,
      server: selectedServer,
      restored: false,
    }
    setSession(nextSession)
    go('home')
    notify('连接就绪，登录会话已保存')
  }

  const changeServer = () => {
    if (isNative) callNative('clearSession')
    setSession(null)
    go('connect')
  }

  const changeAccount = () => {
    if (isNative) callNative('clearSession')
    setSession(null)
    setAuthMode('password')
    go('auth')
  }

  const login = (username, password, remember) => {
    if (!isNative) {
      window.setTimeout(() => finishLogin(username), 820)
      return
    }
    callNative('login', selectedServer.host, username, password, remember)
  }

  const beginQuickConnect = () => {
    if (isNative) callNative('startQuickConnect', selectedServer.host)
  }

  const cancelQuickConnect = () => {
    if (isNative) callNative('cancelQuickConnect')
    setAuthMode('password')
  }

  const changeDisplayMode = (mode) => {
    setDisplayMode(mode)
    if (isNative) {
      callNative('selectDisplayMode', mode === 'stereo' ? 'stereo_screen' : 'mirror_2d')
    }
  }

  const openTouchpad = () => {
    if (isNative && !nativeState?.touchpadReady) {
      notify(nativeState?.glassesRuntimeState === 'error'
        ? nativeState.message || '眼镜端媒体库连接失败，请先检查服务器地址和网络'
        : nativeState?.glassesConnected
          ? '眼镜画面或媒体库仍在启动，请稍候'
          : '连接 RayNeo Air 后即可使用触控板')
      return
    }
    go('touchpad')
  }

  const resetPreferences = () => {
    changeDisplayMode('mirror')
    setHaptics(true)
    notify('偏好已恢复默认')
  }

  const moveButtonOptics = (event) => {
    const target = event.target instanceof Element ? event.target : null
    const button = target?.closest('button')
    if (!button || button.classList.contains('sheet-scrim')) return

    const sample = opticsSampleRef.current || {}
    sample.button = button
    sample.clientX = event.clientX
    sample.clientY = event.clientY
    opticsSampleRef.current = sample
    if (opticsFrameRef.current) return

    opticsFrameRef.current = window.requestAnimationFrame(() => {
      opticsFrameRef.current = 0
      const latest = opticsSampleRef.current
      if (!latest?.button?.isConnected) return

      if (opticsButtonRef.current !== latest.button) {
        opticsButtonRef.current = latest.button
        opticsRectRef.current = null
      }
      const rect = opticsRectRef.current || latest.button.getBoundingClientRect()
      opticsRectRef.current = rect
      const normalizedX = Math.max(-1, Math.min(1, ((latest.clientX - rect.left) / rect.width - 0.5) * 2))
      const normalizedY = Math.max(-1, Math.min(1, ((latest.clientY - rect.top) / rect.height - 0.5) * 2))
      const angle = 135 + normalizedX * 18 + normalizedY * 8
      const scaleX = 1 + Math.abs(normalizedX) * 0.008 - Math.abs(normalizedY) * 0.004
      const scaleY = 1 + Math.abs(normalizedY) * 0.008 - Math.abs(normalizedX) * 0.004

      latest.button.style.setProperty('--glass-x', `${50 + normalizedX * 31}%`)
      latest.button.style.setProperty('--glass-y', `${48 + normalizedY * 30}%`)
      latest.button.style.setProperty('--glass-angle', `${angle}deg`)
      latest.button.style.setProperty('--glass-shift-x', `${normalizedX * 1.35}px`)
      latest.button.style.setProperty('--glass-shift-y', `${normalizedY * 0.8}px`)
      latest.button.style.setProperty('--glass-scale-x', scaleX.toFixed(4))
      latest.button.style.setProperty('--glass-scale-y', scaleY.toFixed(4))
    })
  }

  const resetButtonOptics = (event) => {
    const target = event.target instanceof Element ? event.target : null
    const button = target?.closest('button')
    if (!button || (event.relatedTarget && button.contains(event.relatedTarget))) return
    if (opticsSampleRef.current?.button === button) opticsSampleRef.current.button = null
    if (opticsButtonRef.current === button) {
      opticsButtonRef.current = null
      opticsRectRef.current = null
    }
    button.style.setProperty('--glass-x', '50%')
    button.style.setProperty('--glass-y', '48%')
    button.style.setProperty('--glass-angle', '135deg')
    button.style.setProperty('--glass-shift-x', '0px')
    button.style.setProperty('--glass-shift-y', '0px')
    button.style.setProperty('--glass-scale-x', '1')
    button.style.setProperty('--glass-scale-y', '1')
  }

  return (
    <div className={`prototype-shell ${screen === 'touchpad' ? 'is-touchpad' : ''} ${isNative ? 'is-native' : ''}`}>
      <AmbientBackdrop dark={screen === 'touchpad'} />
      <main
        className="phone-stage"
        onPointerMove={moveButtonOptics}
        onPointerOut={resetButtonOptics}
      >
        <GlassOptics />
        {screen !== 'touchpad' && <StatusBar />}

        <div className="screen-stack" key={screen}>
          {screen === 'connect' && (
            <ConnectScreen
              session={session}
              servers={servers}
              scanning={Boolean(nativeState?.discoveryScanning)}
              discoveryMessage={nativeState?.discoveryMessage || ''}
              onRestore={() => go('home')}
              onChoose={chooseServer}
              onManual={() => setManualOpen(true)}
              onScan={isNative ? () => callNative('scan') : null}
              notify={notify}
            />
          )}

          {screen === 'auth' && (
            <AuthScreen
              server={selectedServer}
              mode={authMode}
              setMode={setAuthMode}
              onBack={() => go('connect')}
              onComplete={finishLogin}
              onLogin={login}
              onQuickStart={beginQuickConnect}
              onQuickCancel={cancelQuickConnect}
              onCopyCode={() => callNative('copyQuickConnectCode')}
              onOpenAuthorization={() => callNative('openQuickConnectAuthorization')}
              nativeState={nativeState}
              isNative={isNative}
              notify={notify}
            />
          )}

          {screen === 'home' && (
            <HomeScreen
              session={session}
              server={selectedServer}
              displayMode={displayMode}
              setDisplayMode={changeDisplayMode}
              onTouchpad={openTouchpad}
              onRetry={() => callNative('retryGlasses')}
              onSettings={() => go('settings')}
              deviceState={nativeState}
              notify={notify}
            />
          )}

          {screen === 'settings' && (
            <SettingsScreen
              session={session}
              server={selectedServer}
              displayMode={displayMode}
              setDisplayMode={changeDisplayMode}
              haptics={haptics}
              setHaptics={setHaptics}
              onChangeAccount={changeAccount}
              onChangeServer={changeServer}
              onReset={resetPreferences}
              onShareDiagnostics={() => {
                if (isNative) callNative('shareDiagnostics')
                else notify('原生应用会打开系统分享面板')
              }}
              nativeState={nativeState}
              notify={notify}
            />
          )}

          {screen === 'touchpad' && (
            <TouchpadScreen
              displayMode={displayMode}
              haptics={haptics}
              playback={nativeState?.playback}
              onExit={() => go('home')}
              onCommand={(command) => callNative('remoteCommand', command, haptics)}
              native={isNative}
            />
          )}
        </div>

        {(screen === 'home' || screen === 'settings') && (
          <BottomNav
            active={screen}
            onHome={() => go('home')}
            onTouchpad={openTouchpad}
            onSettings={() => go('settings')}
          />
        )}

        {manualOpen && (
          <ManualServerSheet
            onClose={() => setManualOpen(false)}
            onContinue={(server) => {
              setSelectedServer(server)
              setManualOpen(false)
              setAuthMode('password')
              go('auth')
            }}
          />
        )}

        <Toast message={toast} />
      </main>
    </div>
  )
}

function GlassOptics() {
  return (
    <svg className="glass-optics" aria-hidden="true">
      <defs>
        <filter id="luma-edge-refraction" x="-25%" y="-25%" width="150%" height="150%" colorInterpolationFilters="sRGB">
          <feTurbulence type="fractalNoise" baseFrequency="0.012 0.045" numOctaves="1" seed="7" result="noise" />
          <feDisplacementMap in="SourceGraphic" in2="noise" scale="2.3" xChannelSelector="R" yChannelSelector="B" result="warped" />
          <feColorMatrix in="warped" type="matrix" values="1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0" result="red" />
          <feOffset in="red" dx="-0.5" dy="0" result="redShift" />
          <feColorMatrix in="warped" type="matrix" values="0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0" result="green" />
          <feColorMatrix in="warped" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0" result="blue" />
          <feOffset in="blue" dx="0.65" dy="0.15" result="blueShift" />
          <feBlend in="green" in2="blueShift" mode="screen" result="greenBlue" />
          <feBlend in="redShift" in2="greenBlue" mode="screen" />
        </filter>
        <filter id="luma-surface-refraction" x="-12%" y="-18%" width="124%" height="136%" colorInterpolationFilters="sRGB">
          <feTurbulence type="fractalNoise" baseFrequency="0.009 0.028" numOctaves="1" seed="11" result="surfaceNoise" />
          <feDisplacementMap in="SourceGraphic" in2="surfaceNoise" scale="4.5" xChannelSelector="R" yChannelSelector="B" />
        </filter>
      </defs>
    </svg>
  )
}

function AmbientBackdrop({ dark }) {
  return (
    <div className={`ambient ${dark ? 'ambient--dark' : ''}`} aria-hidden="true">
      <div className="ambient__wash" />
      <div className="ambient__orb ambient__orb--one" />
      <div className="ambient__orb ambient__orb--two" />
      <div className="ambient__grain" />
    </div>
  )
}

function StatusBar() {
  return (
    <div className="status-bar" aria-hidden="true">
      <span>09:41</span>
      <div className="status-icons">
        <span className="signal-bars"><i /><i /><i /><i /></span>
        <Wifi size={14} strokeWidth={2.3} />
        <span className="battery"><i /></span>
      </div>
    </div>
  )
}

function Brand({ compact = false }) {
  return (
    <div className={`brand ${compact ? 'brand--compact' : ''}`}>
      <span className="brand-mark" aria-hidden="true">
        <i className="brand-mark__ring" />
        <i className="brand-mark__drop" />
      </span>
      <span>
        <strong>JELLYFIN</strong>
        <small>RAYNEO</small>
      </span>
    </div>
  )
}

function ConnectScreen({
  session,
  servers,
  scanning: nativeScanning,
  discoveryMessage,
  onRestore,
  onChoose,
  onManual,
  onScan,
  notify,
}) {
  const [demoScanning, setDemoScanning] = useState(false)
  const [scanRound, setScanRound] = useState(0)
  const scanning = onScan ? nativeScanning : demoScanning

  const scan = () => {
    if (scanning) return
    if (onScan) {
      onScan()
      return
    }
    setDemoScanning(true)
    window.setTimeout(() => {
      setDemoScanning(false)
      setScanRound((round) => round + 1)
      notify('扫描完成，找到 2 台服务器')
    }, 1350)
  }

  return (
    <section className="screen connect-screen">
      <header className="top-row">
        <Brand />
        <button className="icon-button glass-soft" aria-label="更多选项" onClick={() => notify('Jellyfin for RayNeo · 手机伴侣')}>
          <MoreHorizontal size={20} />
        </button>
      </header>

      <div className="art-hero glass-panel">
        <img src={assetUrl('liquid-blue.png')} alt="冰蓝色流体抽象艺术" />
        <div className="art-hero__refraction" />
        <div className="art-hero__copy">
          <span className="eyebrow light">JELLYFIN COMPANION</span>
          <h1>让影像<br />穿过玻璃</h1>
          <p>Jellyfin × RayNeo Air</p>
        </div>
        <div className="art-hero__glint" />
      </div>

      {session && (
        <button className="restore-card glass-panel pressable" onClick={onRestore}>
          <span className="server-orb server-orb--ready"><Zap size={18} /></span>
          <span className="restore-card__copy">
            <small>已保存的会话</small>
            <strong>{session.server?.name ?? 'Jellyfin 媒体库'}</strong>
            <em>{session.username} · 可直接恢复</em>
          </span>
          <ChevronRight size={20} />
        </button>
      )}

      <div className="section-heading">
        <div>
          <span className="eyebrow">LOCAL NETWORK</span>
          <h2>选择媒体服务器</h2>
        </div>
        <button className={`scan-button ${scanning ? 'is-scanning' : ''}`} onClick={scan}>
          <RefreshCw size={15} />
          {scanning ? '发现中' : '重新扫描'}
        </button>
      </div>

      <div className="radar-line" aria-hidden="true">
        <span className={scanning ? 'is-active' : ''} />
      </div>

      <div className="server-list" key={scanRound}>
        {servers.map((server, index) => (
          <button
            className="server-card glass-panel pressable stagger-in"
            style={{ '--delay': `${index * 90}ms` }}
            key={server.id}
            onClick={() => onChoose(server)}
          >
            <span className="server-orb">
              <Server size={20} strokeWidth={1.8} />
              <i />
            </span>
            <span className="server-card__body">
              <span className="server-card__title">
                <strong>{server.name}</strong>
                <small><Radio size={11} /> {server.latency || '局域网'}</small>
              </span>
              <span className="server-card__host">{server.host}</span>
              <span className="server-card__meta">{server.detail || 'Jellyfin 服务器'} · 局域网</span>
            </span>
            <ChevronRight className="muted-icon" size={20} />
          </button>
        ))}
        {servers.length === 0 && (
          <div className={`scan-empty glass-panel ${scanning ? 'is-scanning' : ''}`}>
            <span className="server-orb"><Radar size={20} /></span>
            <span>
              <strong>{scanning ? '正在发现 Jellyfin' : '尚未发现服务器'}</strong>
              <small>{discoveryMessage || '确认手机与服务器处于同一 Wi-Fi，或手动填写地址。'}</small>
            </span>
          </div>
        )}
      </div>

      {servers.length > 0 && discoveryMessage && (
        <p className="discovery-message">{discoveryMessage}</p>
      )}

      <button className="manual-card pressable" onClick={onManual}>
        <span className="manual-card__icon"><Plus size={20} /></span>
        <span>
          <strong>手动填写地址</strong>
          <small>使用域名、IP 或反向代理地址</small>
        </span>
        <ArrowRight size={18} />
      </button>

      <div className="privacy-note">
        <ShieldCheck size={15} />
        发现过程仅在当前局域网内进行
      </div>
    </section>
  )
}

function AuthScreen({
  server,
  mode,
  setMode,
  onBack,
  onComplete,
  onLogin,
  onQuickStart,
  onQuickCancel,
  onCopyCode,
  onOpenAuthorization,
  nativeState,
  isNative,
  notify,
}) {
  const [passwordVisible, setPasswordVisible] = useState(false)
  const [username, setUsername] = useState(nativeState?.username || '')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(true)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!isNative) return
    setLoading(Boolean(nativeState?.busy))
    if (!username && nativeState?.username) setUsername(nativeState.username)
  }, [isNative, nativeState?.busy, nativeState?.username])

  const login = () => {
    if (!username.trim()) {
      notify('请填写 Jellyfin 用户名')
      return
    }
    setLoading(true)
    onLogin(username.trim(), password, remember)
    setPassword('')
  }

  const busy = loading || Boolean(nativeState?.busy)

  return (
    <section className="screen auth-screen">
      <header className="subpage-header">
        <button className="icon-button glass-soft" onClick={onBack} aria-label="返回">
          <ArrowLeft size={20} />
        </button>
        <div className="subpage-header__title">
          <strong>连接 Jellyfin</strong>
          <span>{server.host}</span>
        </div>
        <span className="secure-pill"><LockKeyhole size={12} /> 安全</span>
      </header>

      <div className="auth-art glass-panel">
        <img src={assetUrl('liquid-blue.png')} alt="" />
        <div className="auth-art__glass">
          <span className="server-orb server-orb--light"><Link2 size={21} /></span>
          <div>
            <small>正在登录</small>
            <strong>{server.name}</strong>
          </div>
          <i className="connection-wave" />
        </div>
      </div>

      <div className="auth-tabs glass-soft" role="tablist">
        <button className={mode === 'password' ? 'is-active' : ''} onClick={() => setMode('password')}>
          账号密码
        </button>
        <button className={mode === 'quick' ? 'is-active' : ''} onClick={() => setMode('quick')}>
          Quick Connect
        </button>
        <span className={`auth-tabs__indicator auth-tabs__indicator--${mode}`} />
      </div>

      {mode === 'password' ? (
        <div className="auth-content mode-enter" key="password">
          <div className="form-heading">
            <span className="eyebrow">WELCOME BACK</span>
            <h2>登录你的媒体库</h2>
            <p>凭据只会发送至你选择的 Jellyfin 服务器。</p>
          </div>

          <label className="field glass-panel">
            <UserRound size={19} />
            <span>
              <small>用户名</small>
              <input value={username} onChange={(event) => setUsername(event.target.value)} autoComplete="username" />
            </span>
          </label>

          <label className="field glass-panel">
            <KeyRound size={19} />
            <span>
              <small>密码</small>
              <input
                type={passwordVisible ? 'text' : 'password'}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
              />
            </span>
            <button type="button" onClick={() => setPasswordVisible((visible) => !visible)} aria-label="显示或隐藏密码">
              {passwordVisible ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </label>

          <button className="remember-row" onClick={() => setRemember((value) => !value)}>
            <span className={`check-box ${remember ? 'is-checked' : ''}`}>
              {remember && <Check size={13} strokeWidth={3} />}
            </span>
            <span>
              <strong>保存登录会话</strong>
              <small>下次自动恢复，无需重新输入</small>
            </span>
          </button>

          <button className={`primary-button pressable ${busy ? 'is-loading' : ''}`} onClick={login} disabled={busy}>
            <span>{busy ? '正在建立安全连接' : '登录并连接'}</span>
            {busy ? <i className="button-loader" /> : <ArrowRight size={19} />}
          </button>
          {isNative && nativeState?.message && (
            <p className={`native-status ${nativeState.isError ? 'is-error' : ''}`}>{nativeState.message}</p>
          )}
        </div>
      ) : (
        <QuickConnect
          onComplete={() => onComplete('demo')}
          onStart={onQuickStart}
          onCancel={onQuickCancel}
          onCopy={onCopyCode}
          onOpenAuthorization={onOpenAuthorization}
          nativeState={nativeState}
          isNative={isNative}
          notify={notify}
        />
      )}
    </section>
  )
}

function QuickConnect({
  onComplete,
  onStart,
  onCancel,
  onCopy,
  onOpenAuthorization,
  nativeState,
  isNative,
  notify,
}) {
  const [copied, setCopied] = useState(false)
  const code = isNative ? formatQuickCode(nativeState?.quickConnectCode) : '7RV · 4DP'
  const waitingForCode = isNative && !code

  useEffect(() => {
    if (isNative && !nativeState?.busy && !nativeState?.quickConnectCode) onStart()
  }, [])

  const copyCode = async () => {
    if (!code) return
    if (isNative) {
      onCopy()
    }
    try {
      if (!isNative) await navigator.clipboard.writeText(code.replace(/\s|·/g, ''))
    } catch {
      // Clipboard access can be restricted in embedded previews; visual feedback still demonstrates the action.
    }
    setCopied(true)
    notify('登录码已复制')
    window.setTimeout(() => setCopied(false), 1600)
  }

  return (
    <div className="quick-content mode-enter" key="quick">
      <div className="form-heading">
        <span className="eyebrow">PASSWORDLESS</span>
        <h2>在已登录设备上确认</h2>
        <p>打开 Jellyfin 授权页面，然后输入这组一次性登录码。</p>
      </div>

      <div className="quick-code glass-panel">
        <div className="quick-code__label">
          <span><Radar size={15} /> 登录码</span>
          <i>{waitingForCode ? '正在申请' : '等待确认'}</i>
        </div>
        <button className="quick-code__value" onClick={copyCode} aria-label="复制登录码">
          {waitingForCode ? <i className="button-loader dark" /> : code}
        </button>
        <button className="copy-button" onClick={copyCode} disabled={waitingForCode}>
          {copied ? <Check size={17} /> : <Copy size={17} />}
          {copied ? '已复制' : '复制登录码'}
        </button>
        <div className="quick-code__halo" />
      </div>

      <ol className="quick-steps">
        <li><i>1</i><span>在手机或电脑上打开 Jellyfin</span></li>
        <li><i>2</i><span>进入 Quick Connect 并输入上方代码</span></li>
      </ol>

      <button
        className="primary-button pressable"
        onClick={() => {
          if (isNative) {
            onOpenAuthorization()
            notify('已打开 Jellyfin 授权页面')
          } else {
            notify('授权页已打开 · 浏览器预览中模拟确认成功')
            window.setTimeout(onComplete, 900)
          }
        }}
        disabled={waitingForCode}
      >
        <span>打开授权页面</span>
        <ExternalLink size={18} />
      </button>

      <button className="text-button" onClick={onCancel}>
        <X size={16} /> 取消快速登录
      </button>
      {isNative && nativeState?.message && (
        <p className={`native-status ${nativeState.isError ? 'is-error' : ''}`}>{nativeState.message}</p>
      )}
    </div>
  )
}

function HomeScreen({
  session,
  server,
  displayMode,
  setDisplayMode,
  onTouchpad,
  onRetry,
  onSettings,
  deviceState,
  notify,
}) {
  const activeServer = session?.server ?? server
  const username = session?.username ?? 'Jellyfin'
  const connected = deviceState ? Boolean(deviceState.glassesConnected) : true
  const displayReady = deviceState ? Boolean(deviceState.glassesPresentationReady) : true
  const mediaReady = deviceState ? Boolean(deviceState.mediaReady) : true
  const runtimeState = deviceState?.glassesRuntimeState || (mediaReady ? 'ready' : displayReady ? 'loading' : 'booting')
  const mediaError = runtimeState === 'error'
  const runtimeErrorLabel = {
    network: 'NETWORK',
    http: 'HTTP',
    response: 'RESPONSE',
    unknown: 'UNKNOWN',
  }[deviceState?.glassesRuntimeErrorCode] || 'UNKNOWN'
  let welcomeTitle = '等待连接 RayNeo Air'
  let connectionLabel = '待连接'
  if (connected) {
    welcomeTitle = '眼镜画面正在启动'
    connectionLabel = '已连接'
  }
  if (displayReady) {
    welcomeTitle = '眼镜画面已启动'
    connectionLabel = '画面已启动'
  }
  if (mediaError) {
    welcomeTitle = '媒体库连接失败'
    connectionLabel = '加载失败'
  }
  if (mediaReady) {
    welcomeTitle = '媒体库已准备就绪'
    connectionLabel = '媒体已连接'
  }

  return (
    <section className="screen home-screen with-nav">
      <header className="top-row home-top">
        <Brand compact />
        <button className="profile-button glass-soft" onClick={onSettings}>
          <span>{profileInitials(username)}</span>
          <i />
        </button>
      </header>

      <div className="welcome-line">
        <div>
          <span className="eyebrow">GOOD MORNING</span>
          <h1>{welcomeTitle}</h1>
        </div>
        <span className={`online-label ${!connected || mediaError ? 'is-offline' : ''}`}><i /> {connectionLabel}</span>
      </div>

      <div className="device-hero glass-panel">
        <img src={assetUrl('luma-device-card-light.png')} alt="明亮的冰玻璃流体背景" />
        <div className="device-hero__mist" />
        <div className="device-hero__head">
          <span className={`connected-pill ${connected ? '' : 'is-offline'}`}><i /> {connected ? '眼镜已连接' : '等待连接眼镜'}</span>
          <button onClick={() => notify(deviceState?.displayMessage || 'RayNeo Air 3S · USB-C 空间显示')} aria-label="设备详情"><MoreHorizontal size={19} /></button>
        </div>
        <div className="device-hero__info">
          <small>RAYNEO AIR 3S</small>
          <strong>空间显示器</strong>
          <span><Zap size={13} fill="currentColor" /> {mediaReady ? '媒体库已就绪' : mediaError ? '媒体库连接失败' : displayReady ? '画面已启动，正在连接媒体库' : connected ? '正在准备画面' : 'USB-C 待连接'}</span>
        </div>
      </div>

      {mediaError && (
        <div className="runtime-status-card is-error" role="alert">
          <span className="runtime-status-card__copy">
            <strong>眼镜端诊断 · {runtimeErrorLabel}</strong>
            <span>{deviceState?.message || '眼镜端加载媒体库失败，请检查服务器地址和当前网络。'}</span>
          </span>
          <button className="runtime-status-card__retry" onClick={onRetry}>
            <RefreshCw size={12} /> 重试
          </button>
        </div>
      )}

      <section className="mode-card glass-panel">
        <div className="card-title-row">
          <div>
            <span className="eyebrow">DISPLAY MODE</span>
            <h2>画面输出</h2>
          </div>
          <SlidersHorizontal size={19} />
        </div>
        <ModeSelector value={displayMode} onChange={setDisplayMode} />
        <p>
          {displayMode === 'mirror'
            ? '镜像手机画面，以标准 2D 比例显示。'
            : '为左右眼分别输出画面，获得立体空间感。'}
        </p>
      </section>

      <button className="touchpad-launch pressable" onClick={onTouchpad}>
        <span className="touchpad-launch__orb"><span /></span>
        <span className="touchpad-launch__copy">
          <small>REMOTE SURFACE</small>
          <strong>进入触控板</strong>
          <em>滑动 · 点击 · 双击</em>
        </span>
        <span className="touchpad-launch__arrow"><ArrowRight size={19} /></span>
        <i className="touchpad-launch__glow" />
      </button>

      <div className="connection-card glass-panel">
        <span className="server-orb server-orb--small"><Server size={17} /></span>
        <span>
          <small>当前媒体会话</small>
          <strong>{activeServer?.name ?? 'Jellyfin 媒体库'}</strong>
          <em>{username} · {activeServer?.host ?? '尚未选择服务器'}</em>
        </span>
        <span className="session-check"><Check size={14} /></span>
      </div>
    </section>
  )
}

function ModeSelector({ value, onChange }) {
  return (
    <div className="mode-selector">
      <button className={value === 'mirror' ? 'is-active' : ''} onClick={() => onChange('mirror')}>
        <span><Monitor size={19} /></span>
        <div><strong>镜像 2D</strong><small>同步手机画面</small></div>
        <i className="radio-check">{value === 'mirror' && <Check size={11} />}</i>
      </button>
      <button className={value === 'stereo' ? 'is-active' : ''} onClick={() => onChange('stereo')}>
        <span><Box size={19} /></span>
        <div><strong>立体屏幕</strong><small>SBS 空间画面</small></div>
        <i className="radio-check">{value === 'stereo' && <Check size={11} />}</i>
      </button>
    </div>
  )
}

function SettingsScreen({
  session,
  server,
  displayMode,
  setDisplayMode,
  haptics,
  setHaptics,
  onChangeAccount,
  onChangeServer,
  onReset,
  onShareDiagnostics,
  nativeState,
  notify,
}) {
  const activeServer = session?.server ?? server
  const username = session?.username ?? nativeState?.username ?? 'Jellyfin'
  const sessionSaved = session?.saved ?? nativeState?.sessionSaved ?? true

  return (
    <section className="screen settings-screen with-nav">
      <header className="settings-header">
        <div>
          <span className="eyebrow">PREFERENCES</span>
          <h1>连接与偏好</h1>
        </div>
        <button className="icon-button glass-soft" onClick={() => notify('所有设置已自动保存')} aria-label="设置说明">
          <CircleHelp size={20} />
        </button>
      </header>

      <div className="account-card glass-panel">
        <div className="account-avatar">{profileInitials(username)}<i /></div>
        <div className="account-card__copy">
          <small>JELLYFIN ACCOUNT</small>
          <strong>{username}</strong>
          <span><i /> 已登录 · {sessionSaved ? '会话已保存' : '仅本次运行'}</span>
        </div>
        <button onClick={onChangeAccount}>更换</button>
      </div>

      <SettingsGroup title="媒体服务器">
        <button className="setting-row" onClick={onChangeServer}>
          <span className="setting-row__icon blue"><Router size={19} /></span>
          <span className="setting-row__copy">
            <strong>{activeServer?.name ?? 'Jellyfin 媒体库'}</strong>
            <small>{activeServer?.host ?? '尚未选择服务器'}</small>
          </span>
          <span className="setting-action">更换 <ChevronRight size={15} /></span>
        </button>
      </SettingsGroup>

      <SettingsGroup title="显示">
        <div className="settings-mode-wrap">
          <ModeSelector value={displayMode} onChange={setDisplayMode} />
        </div>
      </SettingsGroup>

      <SettingsGroup title="触控反馈">
        <button className="setting-row" onClick={() => {
          setHaptics((value) => {
            const next = !value
            if (next) callNative('previewHaptic')
            return next
          })
        }}>
          <span className="setting-row__icon mint"><Vibrate size={19} /></span>
          <span className="setting-row__copy">
            <strong>轻触震动</strong>
            <small>手势完成时给出短促反馈</small>
          </span>
          <Toggle checked={haptics} />
        </button>
        <button className="setting-row" onClick={() => notify('已播放光点反馈预览')}>
          <span className="setting-row__icon pearl"><Sparkles size={19} /></span>
          <span className="setting-row__copy">
            <strong>微光反馈</strong>
            <small>跟随手指的低亮度光点</small>
          </span>
          <span className="setting-value">柔和</span>
        </button>
      </SettingsGroup>

      <SettingsGroup title="诊断">
        <button className="setting-row" onClick={onShareDiagnostics}>
          <span className="setting-row__icon blue"><Share2 size={19} /></span>
          <span className="setting-row__copy">
            <strong>分享诊断日志</strong>
            <small>已脱敏，可一键分享到 QQ 等应用</small>
          </span>
          <span className="setting-action">分享 <ChevronRight size={15} /></span>
        </button>
      </SettingsGroup>

      <button className="reset-button" onClick={onReset}>
        <RotateCcw size={16} /> 恢复默认偏好
      </button>

      <p className="version-copy">JELLYFIN FOR RAYNEO · COMPANION</p>
    </section>
  )
}

function SettingsGroup({ title, children }) {
  return (
    <section className="settings-group glass-panel">
      <h2>{title}</h2>
      <div>{children}</div>
    </section>
  )
}

function Toggle({ checked }) {
  return <span className={`toggle ${checked ? 'is-on' : ''}`}><i /></span>
}

function BottomNav({ active, onHome, onTouchpad, onSettings }) {
  return (
    <nav className="bottom-nav glass-panel">
      <button className={active === 'home' ? 'is-active' : ''} onClick={onHome}>
        <span><Glasses size={20} /></span>
        <small>设备</small>
      </button>
      <button className="nav-primary" onClick={onTouchpad}>
        <span><i /></span>
        <small>触控</small>
      </button>
      <button className={active === 'settings' ? 'is-active' : ''} onClick={onSettings}>
        <span><Settings2 size={20} /></span>
        <small>设置</small>
      </button>
    </nav>
  )
}

function TouchpadScreen({ displayMode, haptics, playback, onExit, onCommand, native }) {
  const surfaceRef = useRef(null)
  const glowRef = useRef(null)
  const point = useRef({ x: 50, y: 50, tx: 50, ty: 50, vx: 0, vy: 0 })
  const glowFrameRef = useRef(0)
  const surfaceRectRef = useRef(null)
  const pointerStart = useRef(null)
  const lastTap = useRef(0)
  const tapTimer = useRef(null)
  const hideTimer = useRef(null)
  const [pressed, setPressed] = useState(false)
  const [feedback, setFeedback] = useState('')
  const [introVisible, setIntroVisible] = useState(true)

  const animateGlow = () => {
    glowFrameRef.current = 0
    const p = point.current
    p.vx = (p.vx + (p.tx - p.x) * 0.075) * 0.72
    p.vy = (p.vy + (p.ty - p.y) * 0.075) * 0.72
    p.x += p.vx
    p.y += p.vy

    const settled = Math.abs(p.tx - p.x) < 0.002
      && Math.abs(p.ty - p.y) < 0.002
      && Math.abs(p.vx) < 0.002
      && Math.abs(p.vy) < 0.002
    if (settled) {
      p.x = p.tx
      p.y = p.ty
      p.vx = 0
      p.vy = 0
    }
    if (glowRef.current) {
      glowRef.current.style.transform = `translate3d(${p.x}vw, ${p.y}vh, 0) translate(-50%, -50%)`
    }
    if (!settled) glowFrameRef.current = window.requestAnimationFrame(animateGlow)
  }

  const requestGlowAnimation = () => {
    if (!glowFrameRef.current) {
      glowFrameRef.current = window.requestAnimationFrame(animateGlow)
    }
  }

  useEffect(() => {
    const invalidateSurfaceRect = () => {
      surfaceRectRef.current = null
    }

    requestGlowAnimation()
    window.addEventListener('resize', invalidateSurfaceRect)
    hideTimer.current = window.setTimeout(() => setIntroVisible(false), 4200)
    return () => {
      window.removeEventListener('resize', invalidateSurfaceRect)
      if (glowFrameRef.current) {
        window.cancelAnimationFrame(glowFrameRef.current)
        glowFrameRef.current = 0
      }
      window.clearTimeout(hideTimer.current)
      window.clearTimeout(tapTimer.current)
    }
  }, [])

  const vibrate = (pattern = 8) => {
    if (haptics && navigator.vibrate) navigator.vibrate(pattern)
  }

  const emitCommand = (command, pattern = 8) => {
    if (native) {
      onCommand(command)
    } else {
      vibrate(pattern)
    }
  }

  const updateTarget = (event) => {
    const rect = surfaceRectRef.current || surfaceRef.current.getBoundingClientRect()
    surfaceRectRef.current = rect
    point.current.tx = ((event.clientX - rect.left) / rect.width) * 100
    point.current.ty = ((event.clientY - rect.top) / rect.height) * 100
    requestGlowAnimation()
  }

  const showFeedback = (value) => {
    setFeedback('')
    requestAnimationFrame(() => setFeedback(value))
    window.setTimeout(() => setFeedback(''), 520)
  }

  const onPointerDown = (event) => {
    event.currentTarget.setPointerCapture?.(event.pointerId)
    surfaceRectRef.current = null
    updateTarget(event)
    pointerStart.current = { x: event.clientX, y: event.clientY, time: Date.now() }
    setPressed(true)
    setIntroVisible(false)
  }

  const onPointerMove = (event) => {
    if (!pointerStart.current) return
    updateTarget(event)
  }

  const onPointerUp = (event) => {
    if (!pointerStart.current) return
    updateTarget(event)
    setPressed(false)
    const dx = event.clientX - pointerStart.current.x
    const dy = event.clientY - pointerStart.current.y
    const distance = Math.hypot(dx, dy)
    pointerStart.current = null
    surfaceRectRef.current = null

    if (distance > 46) {
      const horizontal = Math.abs(dx) > Math.abs(dy)
      const direction = horizontal ? (dx > 0 ? 'RIGHT' : 'LEFT') : (dy > 0 ? 'DOWN' : 'UP')
      showFeedback(direction)
      emitCommand(direction.toLowerCase(), 10)
      return
    }

    const now = Date.now()
    if (now - lastTap.current < 330) {
      window.clearTimeout(tapTimer.current)
      lastTap.current = 0
      showFeedback('BACK')
      emitCommand('back', [8, 35, 8])
      return
    }

    lastTap.current = now
    tapTimer.current = window.setTimeout(() => {
      showFeedback('CONFIRM')
      emitCommand('submit', 8)
      lastTap.current = 0
    }, 335)
  }

  const feedbackGlyph = useMemo(() => {
    const glyphs = { UP: '↑', DOWN: '↓', LEFT: '←', RIGHT: '→', BACK: '↩', CONFIRM: '·' }
    return glyphs[feedback] ?? ''
  }, [feedback])

  const playbackState = [
    'preparing',
    'buffering',
    'playing',
    'paused',
    'ended',
    'error',
  ].includes(playback?.state)
    ? playback.state
    : 'stopped'
  const playbackLabels = {
    preparing: '正在准备',
    buffering: '正在缓冲',
    playing: '正在播放',
    paused: '已暂停',
    ended: '播放结束',
    error: '播放出错',
    stopped: '未在播放',
  }
  const durationTicks = Math.max(0, Number(playback?.durationTicks || 0))
  const positionTicks = Math.max(0, Number(playback?.positionTicks || 0))
  const playbackProgress = durationTicks > 0
    ? Math.min(100, positionTicks / durationTicks * 100)
    : 0
  const showPlayback = playbackState !== 'stopped'
    && Boolean(playback?.title || playback?.itemId)

  return (
    <section
      ref={surfaceRef}
      className={`touchpad-screen ${pressed ? 'is-pressed' : ''}`}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={() => {
        pointerStart.current = null
        surfaceRectRef.current = null
        setPressed(false)
      }}
    >
      <img className="touchpad-texture" src={assetUrl('luma-touchpad-void.png')} alt="" draggable="false" />
      <div ref={glowRef} className="finger-glow"><i /></div>
      <div className="touchpad-grain" />

      <header className="touchpad-top">
        <button
          onPointerDown={(event) => event.stopPropagation()}
          onClick={onExit}
          aria-label="退出触控板"
        >
          <X size={15} />
        </button>
        <span><i /> RAYNEO AIR 3S</span>
        <em>{displayMode === 'stereo' ? '3D' : '2D'}</em>
      </header>

      {showPlayback && (
        <aside
          className={`touchpad-playback is-${playbackState}`}
          style={{ '--playback-progress': `${playbackProgress}%` }}
          aria-live="polite"
        >
          <span className="touchpad-playback__status">
            <i /> {playbackLabels[playbackState]}
          </span>
          <strong>{playback.title}</strong>
          {playback.subtitle && <small>{playback.subtitle}</small>}
          <div className="touchpad-playback__timeline"><i /></div>
          <div className="touchpad-playback__meta">
            <span>{formatPlaybackTime(positionTicks)} / {formatPlaybackTime(durationTicks)}</span>
            <em>{playback.playMethod === 'Transcode' ? '服务器转码' : '直接播放'}</em>
          </div>
        </aside>
      )}

      <div className={`touch-feedback ${feedback ? 'is-visible' : ''}`}>
        <span>{feedbackGlyph}</span>
        <small>{feedback === 'CONFIRM' ? '确认' : feedback === 'BACK' ? '返回' : feedback ? `向${{ UP: '上', DOWN: '下', LEFT: '左', RIGHT: '右' }[feedback]}` : ''}</small>
      </div>

      <div className={`touchpad-intro ${introVisible ? 'is-visible' : ''}`}>
        <span className="touchpad-intro__mark"><i /></span>
        <strong>触控已就绪</strong>
        <small>在任意位置开始</small>
      </div>

      <footer className={introVisible ? 'is-visible' : ''}>
        滑动移动 &nbsp;·&nbsp; 单击确认 &nbsp;·&nbsp; 双击返回
      </footer>
    </section>
  )
}

function ManualServerSheet({ onClose, onContinue }) {
  const [address, setAddress] = useState('')

  const submit = () => {
    const clean = address.trim()
    if (!clean) return
    const host = clean.replace(/\/+$/, '')
    onContinue({
      id: 'manual',
      name: host.replace(/^https?:\/\//i, '') || '自定义媒体库',
      host,
      detail: '手动地址',
      latency: '--',
      strength: 3,
    })
  }

  return (
    <div className="sheet-layer" role="dialog" aria-modal="true" aria-label="手动添加服务器">
      <button className="sheet-scrim" onClick={onClose} aria-label="关闭" />
      <section className="bottom-sheet">
        <div className="sheet-handle" />
        <div className="sheet-title">
          <div>
            <span className="eyebrow">MANUAL CONNECTION</span>
            <h2>添加服务器地址</h2>
          </div>
          <button className="icon-button glass-soft" onClick={onClose}><X size={18} /></button>
        </div>
        <p>支持域名、IPv4 和 IPv6；IPv6 带端口时需要使用方括号。</p>
        <label className="address-field">
          <Link2 size={18} />
          <span>
            <small>Jellyfin 地址</small>
            <input
              value={address}
              onChange={(event) => setAddress(event.target.value)}
              placeholder="jellyfin.local:8096"
              autoFocus
            />
          </span>
        </label>
        <div className="address-example">例如：jellyfin.local:8096 或 http://[2001:db8::20]:8096</div>
        <button className="primary-button pressable" onClick={submit}>
          <span>继续登录</span><ArrowRight size={19} />
        </button>
      </section>
    </div>
  )
}

function Toast({ message }) {
  return (
    <div className={`toast ${message ? 'is-visible' : ''}`} aria-live="polite">
      <Check size={15} />
      <span>{message}</span>
    </div>
  )
}

export default App
