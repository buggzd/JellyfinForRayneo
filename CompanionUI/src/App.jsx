import React, { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { applyUiTheme, normalizeUiTheme, readPreviewTheme, savePreviewTheme } from '../../SharedUI/theme.mjs'
import { Toast, usePresence } from './feedback'
import { usePhoneBackground } from './phoneBackground'
import { isTouchpadBackground, normalizeTouchpadBackground, readPreviewTouchpadBackground, savePreviewTouchpadBackground } from './touchpadBackground'
import {
  ArrowLeft,
  ArrowRight,
  Box,
  BookOpen,
  Check,
  ChevronRight,
  ChevronDown,
  Copy,
  ExternalLink,
  Eye,
  EyeOff,
  Glasses,
  Github,
  ImagePlus,
  KeyRound,
  Link2,
  LoaderCircle,
  Info,
  LockKeyhole,
  Monitor,
  MessageSquare,
  MoreHorizontal,
  Plus,
  Palette,
  Radar,
  Radio,
  RefreshCw,
  RotateCcw,
  Router,
  Search,
  Server,
  Settings2,
  Share2,
  ShieldCheck,
  UserRound,
  Trash2,
  Touchpad,
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

const DEFAULT_STEREO_SCREEN = { depthLevel: 1, sizePercent: 90 }
const DEPTH_LABELS = ['基准', '轻微', '适中', '较近']

function validStereoScreen(value) {
  return value && Number.isInteger(value.depthLevel) && value.depthLevel >= 0 && value.depthLevel <= 3
    && Number.isInteger(value.sizePercent) && value.sizePercent >= 80 && value.sizePercent <= 95
}

function sameStereoScreen(first, second) {
  return first?.depthLevel === second?.depthLevel && first?.sizePercent === second?.sizePercent
}

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

function sameServer(server, account) {
  if (server.id && server.id !== 'manual' && server.id === account.serverId) return true
  const normalize = (value) => {
    try {
      const address = /^https?:\/\//i.test(value) ? value : `http://${value}`
      return new URL(address).href.replace(/\/+$/, '')
    } catch {
      return ''
    }
  }
  const address = normalize(server.host)
  return Boolean(address) && address === normalize(account.serverUrl)
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

function normalizeRemoteSearchQuery(value) {
  return String(value || '')
    .normalize('NFKC')
    .toLowerCase()
    .replace(/[^a-z0-9 ]/g, '')
    .replace(/ {2,}/g, ' ')
    .slice(0, 48)
}

function useStoredState(key, initialValue, enabled = true) {
  const [value, setValue] = useState(() => {
    try {
      const stored = enabled ? localStorage.getItem(key) : null
      return stored ? JSON.parse(stored) : initialValue
    } catch {
      return initialValue
    }
  })

  useEffect(() => {
    if (enabled && value?.saved !== false) localStorage.setItem(key, JSON.stringify(value))
    else localStorage.removeItem(key)
  }, [key, value, enabled])

  return [value, setValue]
}

function App() {
  const isNative = useMemo(() => hasNativeBridge(), [])
  const [uiTheme, setUiTheme] = useState(() => isNative
    ? normalizeUiTheme(parseNativePayload(callNative('getState'))?.uiTheme)
    : readPreviewTheme())
  const simpleUi = uiTheme === 'simpleUI'
  const [touchpadPreference, setTouchpadPreference] = useState(() => isNative
    ? parseNativePayload(callNative('getState'))?.touchpadBackground
    : readPreviewTouchpadBackground())
  const touchpadBackground = normalizeTouchpadBackground(touchpadPreference, uiTheme)
  useLayoutEffect(() => { applyUiTheme(uiTheme) }, [uiTheme])
  const [session, setSession] = useStoredState('jellyfin-rayneo-session', null, !isNative)
  const [demoAccounts, setDemoAccounts] = useState(() => {
    if (isNative) return []
    try {
      const stored = JSON.parse(localStorage.getItem('jellyfin-rayneo-accounts') || '[]')
      if (Array.isArray(stored) && stored.length) return stored.slice(0, 12)
    } catch { /* A missing preview history starts empty. */ }
    return session ? [{ ...session, id: session.id || 'demo-restored', saved: true }] : []
  })
  const [pendingRemoval, setPendingRemoval] = useState(null)
  const pendingRemovalRef = useRef(null)
  pendingRemovalRef.current = pendingRemoval
  const activeSessionIdRef = useRef('')
  const accountsAvailableRef = useRef(false)
  const authReturnRef = useRef('connect')
  const demoLoginTimer = useRef(null)
  const [displayMode, setDisplayMode] = useStoredState('jellyfin-rayneo-display', 'stereo')
  const [haptics, setHaptics] = useStoredState('jellyfin-rayneo-haptics', true)
  const [screen, setScreen] = useState(() => (!isNative && session ? 'home' : 'connect'))
  const blackTouchpad = screen === 'touchpad' && touchpadBackground === 'black'
  useLayoutEffect(() => {
    const root = document.documentElement
    if (screen === 'touchpad') root.dataset.touchpadBackground = touchpadBackground
    else delete root.dataset.touchpadBackground
    return () => { delete root.dataset.touchpadBackground }
  }, [screen, touchpadBackground])
  const [selectedServer, setSelectedServer] = useState(DEMO_SERVERS[0])
  const [servers, setServers] = useState(() => (isNative ? [] : DEMO_SERVERS))
  const [authMode, setAuthMode] = useState('password')
  const [manualOpen, setManualOpen] = useState(false)
  const manualMounted = usePresence(manualOpen)
  const manualOpenRef = useRef(false)
  const [toast, setToast] = useState('')
  const [nativeState, setNativeState] = useState(null)
  const [stereoScreen, setStereoScreen] = useState(DEFAULT_STEREO_SCREEN)
  const [demoStereoTestPattern, setDemoStereoTestPattern] = useState(false)
  const stereoScreenRef = useRef(DEFAULT_STEREO_SCREEN)
  const pendingStereoRef = useRef(null)
  const toastTimer = useRef(null)
  const screenRef = useRef(screen)
  const touchpadReadyRef = useRef(false)
  const searchInputActiveRef = useRef(false)
  const lastNativeErrorRef = useRef('')
  const lastNativePayloadRef = useRef('')
  const opticsFrameRef = useRef(0)
  const opticsSampleRef = useRef(null)
  const opticsButtonRef = useRef(null)
  const opticsRectRef = useRef(null)

  useEffect(() => {
    if (!simpleUi) return
    if (opticsFrameRef.current) window.cancelAnimationFrame(opticsFrameRef.current)
    opticsFrameRef.current = 0
    opticsSampleRef.current = null
    opticsButtonRef.current = null
    opticsRectRef.current = null
  }, [simpleUi])

  const accounts = isNative ? nativeState?.accounts || [] : demoAccounts.map((account) => ({
    id: account.id,
    serverUrl: account.server.host,
    serverName: account.server.name,
    serverId: account.server.id,
    username: account.username,
    saved: account.saved !== false,
    active: session?.id === account.id || (!session?.id && session?.username === account.username
      && session?.server.host === account.server.host),
  }))
  accountsAvailableRef.current = accounts.length > 0

  useEffect(() => {
    if (isNative) localStorage.removeItem('jellyfin-rayneo-accounts')
    else localStorage.setItem('jellyfin-rayneo-accounts', JSON.stringify(demoAccounts.filter((account) => account.saved !== false)))
  }, [isNative, demoAccounts])

  const notify = (message, tone = 'info') => {
    window.clearTimeout(toastTimer.current)
    setToast({ text: message, tone })
    toastTimer.current = window.setTimeout(() => setToast(''), tone === 'error' ? 4000 : 2400)
  }

  const background = usePhoneBackground(nativeState, notify)

  const go = (next) => {
    window.scrollTo({ top: 0, behavior: 'instant' })
    if (next === 'touchpad') setToast('')
    if (next !== 'settings') setDemoStereoTestPattern(false)
    screenRef.current = next
    setScreen(next)
  }

  useEffect(() => {
    manualOpenRef.current = manualOpen
  }, [manualOpen])

  useEffect(() => () => {
    window.clearTimeout(toastTimer.current)
    window.clearTimeout(demoLoginTimer.current)
  }, [])

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
      setUiTheme(normalizeUiTheme(next.uiTheme))
      setTouchpadPreference(next.touchpadBackground)
      setDisplayMode(next.displayMode === 'stereo_screen' ? 'stereo' : 'mirror')
      // Ignore an older acknowledgement while the latest slider/button edit is still in flight.
      if (validStereoScreen(next.stereoScreen)
        && (!pendingStereoRef.current || sameStereoScreen(next.stereoScreen, pendingStereoRef.current))) {
        pendingStereoRef.current = null
        stereoScreenRef.current = next.stereoScreen
        setStereoScreen(next.stereoScreen)
      }
      setServers(Array.isArray(next.servers) ? next.servers : [])

      const stateServer = serverFromNative(next)
      const loginServer = serverFromNative({
        serverUrl: next.loginServerUrl,
        serverName: next.loginServerName,
      })
      if (loginServer) setSelectedServer(loginServer)
      const previousSessionId = activeSessionIdRef.current
      activeSessionIdRef.current = next.sessionAvailable ? next.activeSessionId : ''

      if (next.sessionAvailable) {
        const activeServer = stateServer || selectedServer
        setSession({
          id: next.activeSessionId,
          username: next.username || 'Jellyfin',
          server: activeServer,
          restored: true,
          saved: Boolean(next.sessionSaved),
        })
        if (!previousSessionId && (screenRef.current === 'connect' || screenRef.current === 'auth')) go('home')
      } else {
        setSession(null)
        if (['home', 'settings', 'touchpad'].includes(screenRef.current)) {
          go(next.accounts?.length ? 'accounts' : 'connect')
        }
      }

      if (next.state === 'quick_connect_waiting') {
        setAuthMode('quick')
        if (screenRef.current !== 'touchpad') go('auth')
      }

      if (next.isError && next.message && next.message !== lastNativeErrorRef.current) {
        lastNativeErrorRef.current = next.message
        notify(next.message, 'error')
      } else if (!next.isError) {
        lastNativeErrorRef.current = ''
      }

      const touchpadBecameReady = Boolean(next.touchpadReady) && !touchpadReadyRef.current
      touchpadReadyRef.current = Boolean(next.touchpadReady)
      if (touchpadBecameReady && (screenRef.current === 'home' || screenRef.current === 'settings')) {
        go('touchpad')
      }

      const searchInputBecameActive = Boolean(next.searchInputActive) && !searchInputActiveRef.current
      searchInputActiveRef.current = Boolean(next.searchInputActive)
      if (searchInputBecameActive && (screenRef.current === 'home' || screenRef.current === 'settings')) {
        go('touchpad')
      }
    }

    const nativeApi = {
      receiveState,
      openScreen: (requestedScreen) => {
        if (!['home', 'settings', 'accounts', 'connect', 'auth'].includes(requestedScreen)) return
        setManualOpen(false)
        setPendingRemoval(null)
        setAuthMode('password')
        go(requestedScreen)
      },
      handleBack: () => {
        if (pendingRemovalRef.current) {
          setPendingRemoval(null)
          return
        }
        if (manualOpenRef.current) {
          setManualOpen(false)
          return
        }
        if (screenRef.current === 'touchpad' || screenRef.current === 'settings') {
          go('home')
        } else if (screenRef.current === 'auth') {
          callNative('cancelQuickConnect')
          setAuthMode('password')
          go(authReturnRef.current)
        } else if (screenRef.current === 'accounts') {
          go(activeSessionIdRef.current ? 'settings' : 'connect')
        } else if (screenRef.current === 'connect' && accountsAvailableRef.current) {
          go('accounts')
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

  const openLogin = (server, returnScreen = 'connect') => {
    authReturnRef.current = returnScreen
    setSelectedServer(server)
    if (isNative) callNative('selectServer', server.host, server.name)
    setAuthMode('password')
    go('auth')
  }

  const chooseServer = (server) => {
    const matching = accounts.some((account) => sameServer(server, account))
    if (matching) go('accounts')
    else openLogin(server)
  }

  const finishLogin = (username = 'demo', remember = true) => {
    const existing = demoAccounts.find((account) => account.server.host === selectedServer.host && account.username === username)
    if (!existing && demoAccounts.length >= 12) {
      notify('最多保留 12 个账号，请先移除一个不再使用的账号', 'error')
      return
    }
    const nextSession = {
      id: existing?.id || crypto.randomUUID().replaceAll('-', ''),
      username,
      server: selectedServer,
      restored: false,
      saved: remember,
    }
    setDemoAccounts((current) => [...current.filter((account) => account.id !== nextSession.id), nextSession])
    setSession(nextSession)
    go('home')
    notify(remember ? '连接就绪，账号已保存' : '连接就绪，仅本次运行保留', 'success')
  }

  const activateAccount = (account) => {
    if (isNative) callNative('activateSession', account.id)
    else {
      const saved = demoAccounts.find((entry) => entry.id === account.id)
      if (saved) {
        setSession(saved)
        setSelectedServer(saved.server)
        go('home')
      }
    }
  }

  const removeAccount = () => {
    if (!pendingRemoval) return
    if (isNative) callNative('removeSession', pendingRemoval.id)
    else {
      setDemoAccounts((current) => current.filter((account) => account.id !== pendingRemoval.id))
      if (pendingRemoval.active) setSession(null)
    }
    setPendingRemoval(null)
  }

  const leaveLogin = () => {
    window.clearTimeout(demoLoginTimer.current)
    if (isNative) callNative('cancelQuickConnect')
    setAuthMode('password')
    go(authReturnRef.current)
  }

  const login = (username, password, remember) => {
    if (!isNative) {
      demoLoginTimer.current = window.setTimeout(() => finishLogin(username, remember), 820)
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
    if (mode !== 'stereo') setDemoStereoTestPattern(false)
    if (isNative) {
      callNative('selectDisplayMode', mode === 'stereo' ? 'stereo_screen' : 'mirror_2d')
    }
  }

  const changeUiTheme = (theme) => {
    if (theme !== 'liquid-glass' && theme !== 'simpleUI') return
    if (isNative) callNative('selectUiTheme', theme)
    else {
      setUiTheme(theme)
      savePreviewTheme(theme)
    }
  }

  const changeTouchpadBackground = (value) => {
    if (!isTouchpadBackground(value)) return
    if (isNative) callNative('selectTouchpadBackground', value)
    else {
      setTouchpadPreference(value)
      savePreviewTouchpadBackground(value)
    }
  }

  const changeStereoScreen = (patch) => {
    const next = { ...stereoScreenRef.current, ...patch }
    if (!validStereoScreen(next) || sameStereoScreen(next, stereoScreenRef.current)) return
    stereoScreenRef.current = next
    setStereoScreen(next)
    if (isNative) {
      pendingStereoRef.current = next
      callNative('setStereoScreen', JSON.stringify(next))
    }
  }

  const changeStereoTestPattern = (enabled) => {
    if (isNative) callNative('setStereoTestPattern', enabled ? 'on' : 'off')
    else setDemoStereoTestPattern(enabled)
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

  const resetPreferences = async () => {
    if (background.busy || !(await background.clear())) return
    changeUiTheme('liquid-glass')
    changeTouchpadBackground('texture')
    changeStereoTestPattern(false)
    changeStereoScreen(DEFAULT_STEREO_SCREEN)
    changeDisplayMode('mirror')
    setHaptics(true)
    notify('偏好已恢复默认', 'success')
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
    <div className={`prototype-shell ${screen === 'touchpad' ? 'is-touchpad' : ''} ${isNative ? 'is-native' : ''} ${!simpleUi && background.url && screen !== 'touchpad' ? 'has-custom-background' : ''}`}>
      {!simpleUi && !blackTouchpad && <AmbientBackdrop dark={screen === 'touchpad'} background={background.url} />}
      <main
        className="phone-stage"
        onPointerMove={simpleUi || blackTouchpad ? undefined : moveButtonOptics}
        onPointerOut={simpleUi || blackTouchpad ? undefined : resetButtonOptics}
      >
        {!simpleUi && !blackTouchpad && <GlassOptics />}
        <input ref={background.input} type="file" accept="image/jpeg,image/png,image/webp" hidden onChange={background.changeFile} />
        {screen !== 'touchpad' && <StatusBar />}

        <div className="screen-stack" inert={manualOpen || Boolean(pendingRemoval)}>
          {screen === 'connect' && (
            <ConnectScreen
              simpleUi={simpleUi}
              session={session}
              servers={servers}
              scanning={Boolean(nativeState?.discoveryScanning)}
              discoveryMessage={nativeState?.discoveryMessage || ''}
              onRestore={() => go('home')}
              onBack={accounts.length ? () => go('accounts') : null}
              onAccounts={accounts.length ? () => go('accounts') : null}
              onChoose={chooseServer}
              onManual={() => setManualOpen(true)}
              onScan={isNative ? () => callNative('scan') : null}
              notify={notify}
            />
          )}

          {screen === 'auth' && (
            <AuthScreen
              simpleUi={simpleUi}
              server={selectedServer}
              mode={authMode}
              setMode={setAuthMode}
              onBack={leaveLogin}
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
              onTouchpad={openTouchpad}
              onRetry={() => callNative('retryGlasses')}
              onSettings={() => go('settings')}
              onAccounts={() => go('accounts')}
              deviceState={nativeState}
              notify={notify}
            />
          )}

          {screen === 'settings' && (
            <SettingsScreen
              uiTheme={uiTheme}
              background={background}
              onUiThemeChange={changeUiTheme}
              touchpadBackground={touchpadBackground}
              onTouchpadBackgroundChange={changeTouchpadBackground}
              session={session}
              server={selectedServer}
              displayMode={displayMode}
              setDisplayMode={changeDisplayMode}
              stereoScreen={stereoScreen}
              onStereoScreenChange={changeStereoScreen}
              stereoTestPattern={isNative ? Boolean(nativeState?.stereoTestPattern) : demoStereoTestPattern}
              onStereoTestPatternChange={changeStereoTestPattern}
              isNative={isNative}
              haptics={haptics}
              setHaptics={setHaptics}
              onChangeAccount={() => go('accounts')}
              onReset={resetPreferences}
              onShareDiagnostics={() => {
                if (isNative) callNative('shareDiagnostics')
                else notify('原生应用会打开系统分享面板')
              }}
              nativeState={nativeState}
            />
          )}

          {screen === 'accounts' && (
            <AccountsScreen
              accounts={accounts}
              onBack={() => go(session ? 'settings' : 'connect')}
              onAddServer={() => go('connect')}
              onAddAccount={(account) => openLogin(serverFromNative(account), 'accounts')}
              onActivate={activateAccount}
              onRemove={setPendingRemoval}
            />
          )}

          {screen === 'touchpad' && (
            <TouchpadScreen
              simpleUi={simpleUi}
              pureBlack={blackTouchpad}
              displayMode={displayMode}
              haptics={haptics}
              playback={nativeState?.playback}
              searchActive={Boolean(nativeState?.searchInputActive)}
              searchQuery={nativeState?.searchQuery || ''}
              onExit={() => go('home')}
              onCommand={(command) => callNative('remoteCommand', command, haptics)}
              onSearchAction={(command) => callNative('remoteCommand', command, false)}
              onSearchText={(value) => callNative('searchText', value)}
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

        {manualMounted && (
          <ManualServerSheet
            open={manualOpen}
            onClose={() => setManualOpen(false)}
            onContinue={(server) => {
              setManualOpen(false)
              chooseServer(server)
            }}
          />
        )}

        {pendingRemoval && (
          <RemoveAccountDialog account={pendingRemoval} onCancel={() => setPendingRemoval(null)} onConfirm={removeAccount} />
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

function AmbientBackdrop({ dark, background }) {
  if (background && !dark) {
    return (
      <div className="ambient ambient--custom" aria-hidden="true">
        <img src={background} alt="" />
        <div className="ambient__veil" />
      </div>
    )
  }
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
  simpleUi,
  session,
  servers,
  scanning: nativeScanning,
  discoveryMessage,
  onRestore,
  onBack,
  onAccounts,
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
      notify('扫描完成，找到 2 台服务器', 'success')
    }, 1350)
  }

  return (
    <section className="screen connect-screen">
      <header className="top-row">
        {onBack ? <button className="icon-button glass-soft" onClick={onBack} aria-label="返回服务器与账号"><ArrowLeft size={20} /></button> : <Brand />}
        <button className="icon-button glass-soft" aria-label="更多选项" onClick={() => notify('Jellyfin for RayNeo · 手机伴侣')}>
          <MoreHorizontal size={20} />
        </button>
      </header>

      <div className="art-hero glass-panel">
        {!simpleUi && <img src={assetUrl('liquid-blue.png')} alt="冰蓝色流体抽象艺术" />}
        <div className="art-hero__refraction" />
        <div className="art-hero__copy">
          <span className="eyebrow light">JELLYFIN COMPANION</span>
          <h1>{simpleUi ? <>随身影院<br />静享光影</> : <>让影像<br />穿过玻璃</>}</h1>
          <p>Jellyfin × RayNeo Air</p>
        </div>
        <div className="art-hero__glint" />
      </div>

      {onAccounts && (
        <button className="saved-accounts-link glass-panel" onClick={onAccounts}>
          <UserRound size={18} /><span>已登录的服务器与账号</span><ChevronRight size={18} />
        </button>
      )}

      {session && (
        <button className="restore-card glass-panel pressable" onClick={onRestore}>
          <span className="server-orb server-orb--ready"><Zap size={18} /></span>
          <span className="restore-card__copy">
            <small>当前连接</small>
            <strong>{session.server?.name ?? 'Jellyfin 媒体库'}</strong>
            <em>{session.username} · 继续使用，无需登录</em>
          </span>
          <ChevronRight size={20} />
        </button>
      )}

      <div className="section-heading">
        <div>
          <span className="eyebrow">LOCAL NETWORK</span>
          <h2>选择媒体服务器</h2>
        </div>
        <button className={`scan-button ${scanning ? 'is-scanning' : ''}`} onClick={scan} disabled={scanning} aria-busy={scanning}>
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
          <div className={`scan-empty glass-panel ${scanning ? 'is-scanning' : ''}`} role="status">
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
  simpleUi,
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
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(true)
  const [loading, setLoading] = useState(false)

  const login = () => {
    if (loading || nativeState?.busy) return
    if (!username.trim()) {
      notify('请填写 Jellyfin 用户名', 'error')
      return
    }
    if (!isNative) setLoading(true)
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
        {!simpleUi && <img src={assetUrl('liquid-blue.png')} alt="" />}
        <div className="auth-art__glass">
          <span className="server-orb server-orb--light"><Link2 size={21} /></span>
          <div>
            <small>正在登录</small>
            <strong>{server.name}</strong>
          </div>
          <i className="connection-wave" />
        </div>
      </div>

      <div className="auth-tabs glass-soft" role="group" aria-label="登录方式">
        <button aria-pressed={mode === 'password'} className={mode === 'password' ? 'is-active' : ''} disabled={busy} onClick={() => setMode('password')}>
          账号密码
        </button>
        <button aria-pressed={mode === 'quick'} className={mode === 'quick' ? 'is-active' : ''} disabled={busy} onClick={() => setMode('quick')}>
          Quick Connect
        </button>
        <span className={`auth-tabs__indicator auth-tabs__indicator--${mode}`} />
      </div>

      {mode === 'password' ? (
        <form className="auth-content mode-enter" key="password" onSubmit={(event) => { event.preventDefault(); login() }}>
          <div className="form-heading">
            <span className="eyebrow">WELCOME BACK</span>
            <h2>登录你的媒体库</h2>
            <p>凭据只会发送至你选择的 Jellyfin 服务器。</p>
          </div>

          <label className="field glass-panel">
            <UserRound size={19} />
            <span>
              <small>用户名</small>
              <input value={username} onChange={(event) => setUsername(event.target.value)} aria-label="用户名" autoComplete="username" />
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
                aria-label="密码" autoComplete="current-password"
              />
            </span>
            <button type="button" onClick={() => setPasswordVisible((visible) => !visible)} aria-label={passwordVisible ? '隐藏密码' : '显示密码'} aria-pressed={passwordVisible}>
              {passwordVisible ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </label>

          <button type="button" role="switch" aria-checked={remember} className="remember-row" onClick={() => setRemember((value) => !value)}>
            <span className={`check-box ${remember ? 'is-checked' : ''}`}>
              {remember && <Check size={13} strokeWidth={3} />}
            </span>
            <span>
              <strong>保存登录会话</strong>
              <small>下次自动恢复，无需重新输入</small>
            </span>
          </button>

          <button className={`primary-button pressable ${busy ? 'is-loading' : ''}`} type="submit" disabled={busy} aria-busy={busy}>
            <span>{busy ? '正在建立安全连接' : '登录并连接'}</span>
            {busy ? <i className="button-loader" /> : <ArrowRight size={19} />}
          </button>
          {isNative && nativeState?.message && (
            <p className={`native-status ${nativeState.isError ? 'is-error' : ''}`} role="status">{nativeState.message}</p>
          )}
        </form>
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
    notify('登录码已复制', 'success')
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
        <p className={`native-status ${nativeState.isError ? 'is-error' : ''}`} role="status">{nativeState.message}</p>
      )}
    </div>
  )
}

function HomeScreen({
  session,
  server,
  onTouchpad,
  onRetry,
  onSettings,
  onAccounts,
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
  if (connected) welcomeTitle = '正在准备眼镜画面'
  if (displayReady) welcomeTitle = '画面已启动，正在连接媒体库'
  if (mediaError) welcomeTitle = '媒体库连接失败，请重试连接'
  if (mediaReady) welcomeTitle = '一切就绪，开始你的观影时光'

  return (
    <section className={`screen home-screen with-nav ${mediaError ? 'has-runtime-error' : ''}`}>
      <header className="top-row home-top">
        <Brand compact />
        <button className="profile-button glass-soft" onClick={onSettings} aria-label="账户与设置">
          <span>{profileInitials(username)}</span>
          <i />
        </button>
      </header>

      <div className="welcome-line">
        <div>
          <span className="eyebrow">MY DEVICES</span>
          <h1>我的设备</h1>
          <p>{welcomeTitle}</p>
        </div>
      </div>

      <div className="device-hero glass-panel">
        <div className="device-hero__head">
          <span className={`connected-pill ${connected ? '' : 'is-offline'}`}><i /> {connected ? '眼镜已连接' : '等待连接眼镜'}</span>
          <button onClick={() => notify(deviceState?.displayMessage || 'RayNeo Air 3S · USB-C 空间显示')} aria-label="设备详情"><MoreHorizontal size={19} /></button>
        </div>
        <img className="device-hero__product" src={assetUrl('rayneo-air-3s.webp')} alt="RayNeo Air 3S，深色一体式镜片与白色镜腿" />
        <div className="device-hero__info">
          <strong>RayNeo Air 3S</strong>
          <span><Zap size={13} /> {connected ? 'USB-C 已连接' : '通过 USB-C 连接眼镜'}</span>
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

      <button className="connection-card glass-panel pressable" onClick={onAccounts} aria-label="管理服务器与账号">
        <span className="server-orb server-orb--small"><Server size={17} /></span>
        <span>
          <small>当前媒体库</small>
          <strong>{activeServer?.name ?? 'Jellyfin 媒体库'}</strong>
          <em>{username} · 当前账号</em>
        </span>
        <span className="connection-card__action">管理 <ChevronRight size={16} /></span>
      </button>
    </section>
  )
}

function ModeSelector({ value, onChange }) {
  return (
    <div className="mode-selector" role="group" aria-label="画面输出模式">
      <button aria-pressed={value === 'mirror'} className={value === 'mirror' ? 'is-active' : ''} onClick={() => onChange('mirror')}>
        <span><Monitor size={19} /></span>
        <div><strong>镜像 2D</strong><small>双眼相同画面</small></div>
        <i className="radio-check">{value === 'mirror' && <Check size={11} />}</i>
      </button>
      <button aria-pressed={value === 'stereo'} className={value === 'stereo' ? 'is-active' : ''} onClick={() => onChange('stereo')}>
        <span><Box size={19} /></span>
        <div><strong>虚拟银幕</strong><small>可调远近与大小</small></div>
        <i className="radio-check">{value === 'stereo' && <Check size={11} />}</i>
      </button>
    </div>
  )
}

function DisplayModeStatus({ value, state, onRetry }) {
  const transitioning = Boolean(state?.displayModeTransitioning)
  const displayDisabled = Boolean(state?.glassesDisplayDisabled)
  const active = Boolean(state?.displayModeApplied && !transitioning)
  const stereoActive = active && state?.activeDisplayMode === 'stereo_screen'
  const waiting = value === 'stereo' && !stereoActive
  const pending = transitioning || waiting || displayDisabled
  const Icon = transitioning ? LoaderCircle : active && !displayDisabled ? Check : Info
  return (
    <div className={`display-mode-status ${pending ? 'is-pending' : active ? 'is-ready' : 'is-idle'}`} role="status">
      <strong><Icon size={16} className={transitioning ? 'is-spinning' : ''} aria-hidden="true" />{displayDisabled ? '系统尚未启用眼镜输出' : transitioning ? '正在切换眼镜输出…' : stereoActive ? '当前：虚拟银幕已启用' : state?.glassesConnected ? '当前：镜像 2D' : '等待眼镜输出'}</strong>
      <p>{displayDisabled ? '眼镜已连接。请在手机系统中开启“屏幕镜像”，允许眼镜显示画面。HyperOS 在连接或切换模式后可能需要再次手动开启。' : state?.displayMessage || '等待眼镜连接。'}</p>
      {waiting && !transitioning && <p>虚拟银幕尚未启用，远近与大小设置目前只会保存。</p>}
      {waiting && !transitioning && state?.glassesConnected && (
        <div className="display-mode-actions">
          <button type="button" onClick={onRetry}>重新启用</button>
        </div>
      )}
    </div>
  )
}

function ThemeSelector({ value, onChange }) {
  return (
    <fieldset className="theme-selector">
      <legend className="theme-selector__intro">为手机与眼镜，选择同一种氛围</legend>
      <div className="theme-selector__options">
        {[
          { id: 'liquid-glass', name: '液态玻璃', tag: '默认', detail: '通透光影 · 流动质感' },
          { id: 'simpleUI', name: 'simpleUI', tag: '轻简', detail: '静谧展厅 · 轻盈省电' },
        ].map((theme) => (
          <label className={`theme-option ${value === theme.id ? 'is-selected' : ''}`} key={theme.id}>
            <input type="radio" name="ui-theme" value={theme.id} checked={value === theme.id}
              onChange={() => onChange(theme.id)} />
            <span className={`theme-preview theme-preview--${theme.id}`} aria-hidden="true">
              <span className="theme-preview__arch" />
              <span className="theme-preview__caption" />
              <span className="theme-preview__tiles"><i /><i /><i /></span>
            </span>
            <span className="theme-option__title"><strong>{theme.name}</strong><small>{theme.tag}</small></span>
            <span className="theme-option__detail">{theme.detail}</span>
            <span className="theme-option__check" aria-hidden="true">{value === theme.id && <Check size={12} />}</span>
          </label>
        ))}
      </div>
      <p className="theme-selector__note">自动保存，两端同步生效；眼镜重新连接后沿用。</p>
    </fieldset>
  )
}

function SettingsScreen({
  uiTheme,
  background,
  onUiThemeChange,
  touchpadBackground,
  onTouchpadBackgroundChange,
  session,
  server,
  displayMode,
  setDisplayMode,
  stereoScreen,
  onStereoScreenChange,
  stereoTestPattern,
  onStereoTestPatternChange,
  isNative,
  haptics,
  setHaptics,
  onChangeAccount,
  onReset,
  onShareDiagnostics,
  nativeState,
}) {
  const activeServer = session?.server ?? server
  const username = session?.username ?? nativeState?.username ?? 'Jellyfin'
  const sessionSaved = session?.saved ?? nativeState?.sessionSaved ?? true
  const version = nativeState?.appVersionName || __APP_VERSION__
  const versionCode = nativeState?.appVersionCode || __APP_VERSION_CODE__

  return (
    <section className="screen settings-screen with-nav">
      <header className="settings-header">
        <div>
          <span className="eyebrow">MAKE IT YOURS</span>
          <h1>设置</h1>
          <p>你的设备，你的观影方式。</p>
        </div>
        <span className="settings-header__mark glass-soft" aria-hidden="true"><Settings2 size={23} /></span>
      </header>

      <button className="account-card glass-panel settings-account" onClick={onChangeAccount} aria-label="管理服务器与账号">
        <div className="account-avatar">{profileInitials(username)}<i /></div>
        <div className="account-card__copy">
          <small>服务器与账号</small>
          <strong>{username}</strong>
          <span>{activeServer?.name ?? 'Jellyfin 媒体库'} · {sessionSaved ? '登录已保存' : '仅本次运行'}</span>
        </div>
        <ChevronRight size={18} className="settings-account__arrow" />
      </button>

      <SettingsGroup title="外观与交互">
        <SettingsDisclosure icon={Palette} title="界面外观" detail="主题风格与手机背景" value={uiTheme === 'simpleUI' ? 'simpleUI' : 'Liquid UI'}>
          <ThemeSelector value={uiTheme} onChange={onUiThemeChange} />
          {uiTheme === 'liquid-glass' && <BackgroundPicker background={background} />}
        </SettingsDisclosure>
        <SettingsDisclosure icon={Touchpad} title="遥控器背景" detail="纹理氛围或 OLED 纯黑"
          value={touchpadBackground === 'black' ? '纯黑' : '纹理'}>
          <TouchpadBackgroundSelector value={touchpadBackground} onChange={onTouchpadBackgroundChange} />
        </SettingsDisclosure>
        <button className="setting-row" role="switch" aria-checked={haptics} onClick={() => {
          const next = !haptics
          setHaptics(next)
          if (next) callNative('previewHaptic')
        }}>
          <span className="setting-row__icon mint"><Vibrate size={19} /></span>
          <span className="setting-row__copy"><strong>轻触震动</strong><small>触控板手势完成时的短促反馈</small></span>
          <Toggle checked={haptics} />
        </button>
      </SettingsGroup>

      <SettingsGroup title="眼镜显示">
        <SettingsDisclosure icon={Glasses} title="画面输出" detail="显示模式、银幕远近与大小"
          value={displayMode === 'stereo' ? '虚拟银幕' : '镜像 2D'} onClose={() => onStereoTestPatternChange(false)}>
        <div className="settings-mode-wrap">
          <ModeSelector value={displayMode} onChange={setDisplayMode} />
          {isNative
            ? <DisplayModeStatus value={displayMode} state={nativeState} onRetry={() => setDisplayMode(displayMode)} />
            : <p className="stereo-status">演示预览：连接眼镜后可体验虚拟银幕。</p>}
          {displayMode === 'stereo' && (
            <div className="stereo-settings">
              <div className="stereo-setting-label" id="stereo-depth-label">
                <strong>靠近程度</strong><span>{DEPTH_LABELS[stereoScreen.depthLevel]}</span>
              </div>
              <div className="stereo-depth-options" role="group" aria-labelledby="stereo-depth-label">
                {DEPTH_LABELS.map((label, depthLevel) => (
                  <button key={label} type="button" aria-pressed={stereoScreen.depthLevel === depthLevel}
                    onClick={() => onStereoScreenChange({ depthLevel })}>{label}</button>
                ))}
              </div>
              <p className="stereo-help">从「轻微」开始，让整块银幕更靠近。片中物体仍保持原有的 2D 画面。</p>
              <label className="stereo-setting-label" htmlFor="stereo-size">
                <strong>银幕大小</strong><output htmlFor="stereo-size">{stereoScreen.sizePercent}%</output>
              </label>
              <input id="stereo-size" className="stereo-size-range" type="range" min="80" max="95" step="1"
                value={stereoScreen.sizePercent} aria-valuetext={`${stereoScreen.sizePercent}%`}
                onChange={(event) => onStereoScreenChange({ sizePercent: Number(event.target.value) })} />
              <div className="stereo-range-labels"><span>80% · 较小</span><span>95% · 较大</span></div>
              <p className="stereo-help">大小与远近感独立调整。若有重影或不适，先选「基准」或切回镜像 2D。</p>
              <button type="button" className="stereo-test-button" aria-pressed={stereoTestPattern}
                disabled={isNative && !(nativeState?.displayModeApplied && !nativeState?.displayModeTransitioning
                  && nativeState?.activeDisplayMode === 'stereo_screen' && nativeState?.stereoOutput?.stereoReady
                  && nativeState?.glassesPresentationReady)}
                onClick={() => onStereoTestPatternChange(!stereoTestPattern)}>
                <Eye size={16} /> {stereoTestPattern ? '结束左右眼检查' : '检查左右眼'}
              </button>
              {stereoTestPattern && (
                <p className="stereo-help" role="status">
                  {isNative ? '交替闭眼：左眼应看到 L，右眼应看到 R。白框是基准，青色框随银幕移动；逐档靠近时应更靠前。离开设置会结束检查。'
                    : '此处仅预览设置。眼镜上的检查图会显示 L / R、白色基准框与随银幕移动的青色框。'}
                </p>
              )}
            </div>
          )}
        </div>
        </SettingsDisclosure>
      </SettingsGroup>

      <SettingsGroup title="关于与帮助">
        <div className="setting-row setting-row--static">
          <span className="setting-row__icon pearl"><Info size={19} /></span>
          <span className="setting-row__copy">
            <strong>当前版本</strong>
            <small>Jellyfin for RayNeo{nativeState?.appVersionName ? '' : ' · 浏览器预览'}</small>
          </span>
          <span className="app-version"><strong>{version}</strong><small>Build {versionCode}</small></span>
        </div>
        <ProjectSettingLink page="project" icon={Github} title="项目地址" detail="GitHub · 源码与最新动态" />
        <ProjectSettingLink page="issues" icon={MessageSquare} title="反馈问题" detail="提交 Issue，或查看已有反馈" />
        <ProjectSettingLink page="guide" icon={BookOpen} title="使用指南" detail="连接、操作与常见问题" />
        <button className="setting-row" onClick={onShareDiagnostics}>
          <span className="setting-row__icon blue"><Share2 size={19} /></span>
          <span className="setting-row__copy">
            <strong>分享诊断日志</strong>
            <small>导出脱敏日志，帮助排查问题</small>
          </span>
          <ChevronRight size={16} className="setting-chevron" />
        </button>
      </SettingsGroup>

      <button className="reset-button" disabled={background.busy} onClick={onReset}>
        <RotateCcw size={16} /> 恢复默认偏好
      </button>

      <p className="settings-footer">Jellyfin for RayNeo<span>开源第三方客户端 · MIT License</span></p>
    </section>
  )
}

function SettingsDisclosure({ icon: Icon, title, detail, value, onClose, children }) {
  return (
    <details className="settings-disclosure" onToggle={(event) => { if (!event.currentTarget.open) onClose?.() }}>
      <summary className="setting-row">
        <span className="setting-row__icon blue"><Icon size={19} /></span>
        <span className="setting-row__copy"><strong>{title}</strong><small>{detail}</small></span>
        <span className="setting-current">{value}<ChevronDown size={16} /></span>
      </summary>
      <div className="settings-disclosure__content">{children}</div>
    </details>
  )
}

function TouchpadBackgroundSelector({ value, onChange }) {
  return (
    <div className="touchpad-background-selector">
      <div className="touchpad-background-options" role="group" aria-label="遥控器背景">
        {[
          { id: 'texture', title: '纹理', detail: '柔和暗纹与触摸微光' },
          { id: 'black', title: '纯黑', detail: '适合 OLED 屏幕' },
        ].map((option) => (
          <button key={option.id} type="button" aria-pressed={value === option.id} onClick={() => onChange(option.id)}>
            <span className={`touchpad-background-preview is-${option.id}`} aria-hidden="true"><i /><span>轻触 · 滑动</span></span>
            <span className="touchpad-background-option__title">{option.title}<span className="radio-check">{value === option.id && <Check size={10} />}</span></span>
            <small>{option.detail}</small>
          </button>
        ))}
      </div>
      <p>纯黑关闭背景纹理与触摸光晕，保留操作提示和震动反馈。</p>
    </div>
  )
}

function BackgroundPicker({ background }) {
  return (
    <div className="background-picker" aria-busy={background.busy}>
      <div className="background-picker__heading"><strong>手机背景</strong><span>LIQUID UI</span></div>
      <div className="background-picker__body">
        <div className={`background-preview ${background.url ? 'has-image' : ''}`} aria-hidden="true">
          {background.url && <img src={background.url} alt="" />}
          <i /><i /><i />
        </div>
        <div className="background-picker__copy">
          <strong>{background.url ? '自定义背景' : '默认冰蓝'}</strong>
          <p>换一张喜欢的图片，让玻璃映出你的色彩。</p>
          <button className="background-choose" disabled={background.busy} onClick={background.choose}>
            {background.busy ? <LoaderCircle className="is-spinning" size={15} /> : <ImagePlus size={15} />}
            {background.busy ? '正在处理…' : background.url ? '更换图片' : '选择图片'}
          </button>
        </div>
      </div>
      <div className="background-picker__footer">
        <p>图片仅保存在本机，用于 Liquid 手机界面。</p>
        {background.url && <button disabled={background.busy} onClick={background.clear}>恢复默认背景</button>}
      </div>
    </div>
  )
}

function ProjectSettingLink({ page, icon: Icon, title, detail }) {
  const root = 'https://github.com/buggzd/JellyfinForRayneo'
  const url = { project: root, issues: `${root}/issues`, guide: `${root}/blob/main/docs/USER_GUIDE.md` }[page]
  return (
    <a className="setting-row" href={url} target="_blank" rel="noopener noreferrer" onClick={(event) => {
      if (typeof window.JellyfinNative?.openProjectPage === 'function') {
        event.preventDefault()
        callNative('openProjectPage', page)
      }
    }}>
      <span className="setting-row__icon pearl"><Icon size={19} /></span>
      <span className="setting-row__copy"><strong>{title}</strong><small>{detail}</small></span>
      <ExternalLink size={15} className="setting-chevron" />
    </a>
  )
}

function AccountsScreen({ accounts, onBack, onAddServer, onAddAccount, onActivate, onRemove }) {
  const groups = Object.values(accounts.reduce((result, account) => {
    const key = account.serverUrl
    if (!result[key]) result[key] = { server: account, accounts: [] }
    result[key].accounts.push(account)
    return result
  }, Object.create(null)))

  return (
    <section className="screen accounts-screen">
      <header className="subpage-header">
        <button className="icon-button glass-soft" onClick={onBack} aria-label="返回"><ArrowLeft size={20} /></button>
        <div className="subpage-header__title"><strong>服务器与账号</strong><span>{groups.length} 台服务器 · {accounts.length} 个账号</span></div>
        <span className="account-header-icon"><Router size={22} /></span>
      </header>
      <div className="accounts-intro">
        <h1>连接你的媒体库</h1>
        <p>切换已登录账号，无需重复输入密码。添加服务器或账号时，当前连接会保留到登录成功。</p>
      </div>
      {groups.map((group) => (
        <section className="account-server-card glass-panel" key={group.server.serverUrl}>
          <header>
            <span className="server-orb server-orb--small"><Server size={18} /></span>
            <div><h2>{group.server.serverName || 'Jellyfin 媒体库'}</h2><p>{group.server.serverUrl}</p></div>
          </header>
          <div className="saved-account-list">
            {group.accounts.map((account) => (
              <div className={`saved-account-row ${account.active ? 'is-active' : ''}`} key={account.id}>
                <button className="saved-account-select" onClick={() => onActivate(account)} aria-label={`${account.active ? '继续使用' : '切换到'} ${account.username}`}>
                  <span className="saved-account-avatar">{profileInitials(account.username)}</span>
                  <span className="saved-account-copy"><strong>{account.username || 'Jellyfin 用户'}</strong><small>{account.saved ? '登录已保存' : '仅本次运行'}</small></span>
                  <span className="saved-account-state">{account.active ? <><Check size={13} /> 使用中</> : <>切换 <ChevronRight size={14} /></>}</span>
                </button>
                <button className="remove-account-button" onClick={() => onRemove(account)} aria-label={`移除 ${account.username} 的登录`}><Trash2 size={17} /></button>
              </div>
            ))}
          </div>
          <button className="add-account-button" onClick={() => onAddAccount(group.server)}><Plus size={16} /> 添加账号</button>
        </section>
      ))}
      {!accounts.length && <div className="accounts-empty glass-panel"><UserRound size={28} /><strong>还没有已登录账号</strong><p>登录服务器后，账号会显示在这里。</p></div>}
      <button className="primary-button pressable" onClick={onAddServer}><Plus size={18} /><span>添加服务器</span></button>
      {accounts.length >= 12 && <p className="accounts-limit" role="status">已达到 12 个账号的上限，添加前请先移除不再使用的账号。</p>}
    </section>
  )
}

function RemoveAccountDialog({ account, onCancel, onConfirm }) {
  const dialogRef = useRef(null)
  useEffect(() => {
    const dialog = dialogRef.current
    const opener = document.activeElement
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    dialog.showModal()
    return () => {
      dialog.close()
      document.body.style.overflow = overflow
      if (opener?.isConnected) opener.focus({ preventScroll: true })
    }
  }, [])

  return (
    <dialog ref={dialogRef} className="account-dialog" aria-labelledby="remove-account-title" aria-describedby="remove-account-description" onCancel={(event) => { event.preventDefault(); onCancel() }}>
      <h2 id="remove-account-title">移除这个登录账号？</h2>
      <p className="remove-account-name">{account.username} · {account.serverName || 'Jellyfin'}</p>
      <p id="remove-account-description">移除本机保存的登录状态，重新使用需要登录。{account.active ? '当前连接也会断开。' : ''}</p>
      <div className="account-dialog-actions"><button className="secondary-button" autoFocus onClick={onCancel}>取消</button><button className="remove-account-confirm" onClick={onConfirm}>移除登录</button></div>
    </dialog>
  )
}

function SettingsGroup({ title, children }) {
  return (
    <section className="settings-group">
      <h2>{title}</h2>
      <div className="settings-group__body glass-panel">{children}</div>
    </section>
  )
}

function Toggle({ checked }) {
  return <span className={`toggle ${checked ? 'is-on' : ''}`}><i /></span>
}

function BottomNav({ active, onHome, onTouchpad, onSettings }) {
  return (
    <nav className="bottom-nav glass-panel" aria-label="手机导航">
      <button aria-current={active === 'home' ? 'page' : undefined} className={active === 'home' ? 'is-active' : ''} onClick={onHome}>
        <span><Glasses size={20} /></span>
        <small>设备</small>
      </button>
      <button className="nav-primary" onClick={onTouchpad}>
        <span><i /></span>
        <small>触控</small>
      </button>
      <button aria-current={active === 'settings' ? 'page' : undefined} className={active === 'settings' ? 'is-active' : ''} onClick={onSettings}>
        <span><Settings2 size={20} /></span>
        <small>设置</small>
      </button>
    </nav>
  )
}

function TouchpadScreen({
  simpleUi,
  pureBlack,
  displayMode,
  haptics,
  playback,
  searchActive,
  searchQuery,
  onExit,
  onCommand,
  onSearchAction,
  onSearchText,
  native,
}) {
  const surfaceRef = useRef(null)
  const searchInputRef = useRef(null)
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
  const [searchValue, setSearchValue] = useState(() => normalizeRemoteSearchQuery(searchQuery))

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
    if (pureBlack) return
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
  }, [pureBlack])

  useEffect(() => {
    setSearchValue(searchActive ? normalizeRemoteSearchQuery(searchQuery) : '')
  }, [searchActive, searchQuery])

  useEffect(() => {
    if (!searchActive) return undefined
    setIntroVisible(false)
    const timer = window.setTimeout(() => {
      try {
        searchInputRef.current?.focus({ preventScroll: true })
      } catch {
        searchInputRef.current?.focus()
      }
    }, 120)
    return () => window.clearTimeout(timer)
  }, [searchActive])

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
    if (pureBlack) return
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
      className={`touchpad-screen ${pressed ? 'is-pressed' : ''} ${searchActive ? 'is-search-input' : ''}`}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={() => {
        pointerStart.current = null
        surfaceRectRef.current = null
        setPressed(false)
      }}
    >
      {!pureBlack && <img className="touchpad-texture" src={assetUrl('luma-touchpad-void.png')} alt="" draggable="false" />}
      {!pureBlack && <div ref={glowRef} className="finger-glow"><i /></div>}
      {!simpleUi && !pureBlack && <div className="touchpad-grain" />}

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

      {searchActive && (
        <aside
          className="touchpad-search-input"
          onPointerDown={(event) => event.stopPropagation()}
          onPointerMove={(event) => event.stopPropagation()}
          onPointerUp={(event) => event.stopPropagation()}
        >
          <span className="touchpad-search-input__status"><i /> 眼镜搜索已连接</span>
          <label>
            <Search size={18} />
            <input
              ref={searchInputRef}
              type="search"
              inputMode="search"
              enterKeyHint="search"
              autoCapitalize="none"
              autoComplete="off"
              spellCheck="false"
              maxLength={48}
              value={searchValue}
              placeholder="输入拼音首字母、完整拼音或英文"
              aria-label="眼镜端剧集搜索"
              onFocus={() => onSearchAction('search-keyboard-visible')}
              onBlur={() => onSearchAction('search-keyboard-hidden')}
              onKeyDown={(event) => {
                if (event.key !== 'Enter') return
                event.preventDefault()
                onSearchAction('search-submit')
                event.currentTarget.blur()
              }}
              onChange={(event) => {
                const next = normalizeRemoteSearchQuery(event.target.value)
                setSearchValue(next)
                onSearchText(next)
              }}
            />
            {searchValue && (
              <button
                type="button"
                aria-label="清空搜索"
                onPointerDown={(event) => event.preventDefault()}
                onClick={() => {
                  setSearchValue('')
                  onSearchText('')
                  searchInputRef.current?.focus()
                }}
              >
                <X size={15} />
              </button>
            )}
          </label>
          <small>手机键盘输入会实时显示在眼镜中</small>
        </aside>
      )}

      {showPlayback && !searchActive && (
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

      <div className={`touchpad-intro ${introVisible && !searchActive ? 'is-visible' : ''}`}>
        {!pureBlack && <span className="touchpad-intro__mark"><i /></span>}
        <strong>触控已就绪</strong>
        <small>在任意位置开始</small>
      </div>

      <footer className={introVisible || searchActive ? 'is-visible' : ''}>
        {searchActive
          ? '输入完成后点键盘“搜索”，焦点会进入眼镜端结果'
          : '滑动移动 · 单击确认 · 双击返回'}
      </footer>
    </section>
  )
}

function ManualServerSheet({ open, onClose, onContinue }) {
  const [address, setAddress] = useState('')
  const sheetRef = useRef(null)
  const closeRef = useRef(onClose)
  closeRef.current = onClose

  useEffect(() => {
    if (!open) return
    const opener = document.querySelector('.manual-card')
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const keydown = (event) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        closeRef.current()
      }
      if (event.key !== 'Tab') return
      const targets = [...sheetRef.current.querySelectorAll('button:not(:disabled), input')]
      const first = targets[0]
      const last = targets.at(-1)
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last?.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first?.focus()
      }
    }
    document.addEventListener('keydown', keydown)
    return () => {
      document.body.style.overflow = overflow
      document.removeEventListener('keydown', keydown)
      if (opener?.isConnected) opener.focus({ preventScroll: true })
    }
  }, [open])

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
    <div className={`sheet-layer${open ? '' : ' is-leaving'}`} inert={!open} role="dialog" aria-modal="true" aria-hidden={!open} aria-label="手动添加服务器">
      <button className="sheet-scrim" onClick={onClose} aria-label="关闭" tabIndex={-1} />
      <form ref={sheetRef} className="bottom-sheet" onSubmit={(event) => { event.preventDefault(); submit() }}>
        <div className="sheet-handle" />
        <div className="sheet-title">
          <div>
            <span className="eyebrow">MANUAL CONNECTION</span>
            <h2>添加服务器地址</h2>
          </div>
          <button type="button" className="icon-button glass-soft" onClick={onClose} aria-label="关闭添加服务器"><X size={18} /></button>
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
              inputMode="url"
              enterKeyHint="go"
              autoComplete="url"
              autoCapitalize="none"
              spellCheck={false}
              autoFocus
            />
          </span>
        </label>
        <div className="address-example">例如：jellyfin.local:8096 或 http://[2001:db8::20]:8096</div>
        <button className="primary-button pressable" type="submit" disabled={!address.trim()}>
          <span>继续登录</span><ArrowRight size={19} />
        </button>
      </form>
    </div>
  )
}

export default App
