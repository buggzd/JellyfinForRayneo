export type JellyfinSession = {
  serverUrl: string
  serverName: string
  serverVersion: string
  serverId: string
  accessToken: string
  userId: string
  userName: string
  deviceId: string
}

export type RuntimeBootstrap = {
  source: 'android' | 'development' | 'browser'
  displayMode: string
  glassesConnected: boolean
  session: JellyfinSession | null
  error?: string
}

type NativeGlassesBridge = {
  getBootstrapState: () => string
  getHardwareVideoCodecs: () => string
  ready: () => void
  postMessage: (message: string) => void
}

type LucentNativeReceiver = {
  receiveBootstrapState?: (payload: string | RuntimeBootstrap) => void
}

declare global {
  interface Window {
    RayNeoGlasses?: NativeGlassesBridge
    LucentNative?: LucentNativeReceiver
  }
}

const listeners = new Set<(bootstrap: RuntimeBootstrap) => void>()
let latestNativeBootstrap: RuntimeBootstrap | null = null
let cachedHardwareVideoCodecs: readonly string[] | null | undefined

const supportedHardwareVideoCodecs = new Set([
  'h264',
  'hevc',
  'vp8',
  'vp9',
  'av1',
])

function text(value: unknown) {
  return typeof value === 'string' ? value.trim() : ''
}

function normalizeServerUrl(value: unknown) {
  return text(value).replace(/\/+$/, '')
}

function normalizeSession(value: unknown): JellyfinSession | null {
  if (!value || typeof value !== 'object') return null

  const source = value as Record<string, unknown>
  const session: JellyfinSession = {
    serverUrl: normalizeServerUrl(source.serverUrl),
    serverName: text(source.serverName),
    serverVersion: text(source.serverVersion),
    serverId: text(source.serverId),
    accessToken: text(source.accessToken),
    userId: text(source.userId),
    userName: text(source.userName),
    deviceId: text(source.deviceId),
  }

  if (!session.serverUrl || !session.accessToken || !session.userId) return null
  if (!session.deviceId) session.deviceId = 'rayneo-glasses-webview'
  return session
}

function parseBootstrap(value: string | RuntimeBootstrap | unknown): RuntimeBootstrap | null {
  try {
    const parsed = typeof value === 'string' ? JSON.parse(value) as unknown : value
    if (!parsed || typeof parsed !== 'object') return null

    const source = parsed as Record<string, unknown>
    return {
      source: source.source === 'android' ? 'android' : 'browser',
      displayMode: text(source.displayMode) || 'Mirror2D',
      glassesConnected: source.glassesConnected !== false,
      session: normalizeSession(source.session),
    }
  } catch {
    return null
  }
}

function publishNativeBootstrap(value: string | RuntimeBootstrap) {
  const bootstrap = parseBootstrap(value)
  if (!bootstrap) return
  latestNativeBootstrap = bootstrap
  listeners.forEach((listener) => listener(bootstrap))
}

const existingReceiver = window.LucentNative
window.LucentNative = {
  ...existingReceiver,
  receiveBootstrapState: publishNativeBootstrap,
}

function authorizationHeader(deviceId: string, token?: string) {
  const safeDeviceId = deviceId.replace(/["\\]/g, '')
  const values = [
    'MediaBrowser Client="Lucent for RayNeo"',
    'Device="RayNeo Air"',
    `DeviceId="${safeDeviceId}"`,
    'Version="0.1.0"',
  ]
  if (token) values.push(`Token="${token.replace(/["\\]/g, '')}"`)
  return values.join(', ')
}

async function developmentBootstrap(): Promise<RuntimeBootstrap> {
  try {
    const configResponse = await fetch('/__jellyfin-dev-config', { cache: 'no-store' })
    if (!configResponse.ok) {
      throw new Error('未找到开发环境 Jellyfin 配置。')
    }

    const config = await configResponse.json() as Record<string, unknown>
    const serverUrl = normalizeServerUrl(config.serverUrl)
    const username = text(config.username)
    const password = typeof config.password === 'string' ? config.password : ''
    if (!/^https?:\/\//i.test(serverUrl) || !username) {
      throw new Error('开发环境 Jellyfin 配置不完整。')
    }

    const deviceId = 'lucent-rayneo-web-development'
    const headers = {
      'Content-Type': 'application/json',
      'X-Emby-Authorization': authorizationHeader(deviceId),
    }
    const [infoResponse, authenticationResponse] = await Promise.all([
      fetch(`${serverUrl}/System/Info/Public`, { cache: 'no-store' }),
      fetch(`${serverUrl}/Users/AuthenticateByName`, {
        method: 'POST',
        headers,
        body: JSON.stringify({ Username: username, Pw: password }),
      }),
    ])

    if (!infoResponse.ok || !authenticationResponse.ok) {
      throw new Error(authenticationResponse.status === 401
        ? '开发账号认证失败。'
        : `Jellyfin 连接失败（${authenticationResponse.status || infoResponse.status}）。`)
    }

    const publicInfo = await infoResponse.json() as Record<string, unknown>
    const authentication = await authenticationResponse.json() as Record<string, unknown>
    const user = authentication.User as Record<string, unknown> | undefined
    const accessToken = text(authentication.AccessToken)
    const userId = text(user?.Id)
    if (!accessToken || !userId) throw new Error('Jellyfin 没有返回有效会话。')

    return {
      source: 'development',
      displayMode: 'Mirror2D',
      glassesConnected: true,
      session: {
        serverUrl,
        serverName: text(publicInfo.ServerName),
        serverVersion: text(publicInfo.Version),
        serverId: text(authentication.ServerId) || text(publicInfo.Id),
        accessToken,
        userId,
        userName: text(user?.Name) || username,
        deviceId,
      },
    }
  } catch (error) {
    return {
      source: 'development',
      displayMode: 'Mirror2D',
      glassesConnected: true,
      session: null,
      error: error instanceof Error ? error.message : '无法读取开发环境 Jellyfin 会话。',
    }
  }
}

export async function discoverRuntime(): Promise<RuntimeBootstrap> {
  const native = window.RayNeoGlasses
  if (native) {
    if (!latestNativeBootstrap) {
      try {
        latestNativeBootstrap = parseBootstrap(native.getBootstrapState())
      } catch {
        latestNativeBootstrap = null
      }
    }

    try {
      native.ready()
    } catch {
      // The synchronous state above is still usable when the ready callback fails.
    }

    return latestNativeBootstrap ?? {
      source: 'android',
      displayMode: 'Mirror2D',
      glassesConnected: true,
      session: null,
      error: '无法读取手机端 Jellyfin 会话。',
    }
  }

  if (import.meta.env.DEV) return developmentBootstrap()
  return {
    source: 'browser',
    displayMode: 'Mirror2D',
    glassesConnected: true,
    session: null,
    error: '生产网页仅在 RayNeo 眼镜 WebView 中运行。',
  }
}

export function subscribeRuntime(listener: (bootstrap: RuntimeBootstrap) => void) {
  listeners.add(listener)
  if (latestNativeBootstrap) listener(latestNativeBootstrap)
  return () => listeners.delete(listener)
}

export function getNativeHardwareVideoCodecs(): readonly string[] | null {
  if (cachedHardwareVideoCodecs !== undefined) return cachedHardwareVideoCodecs

  const native = window.RayNeoGlasses
  if (!native || typeof native.getHardwareVideoCodecs !== 'function') {
    cachedHardwareVideoCodecs = null
    return cachedHardwareVideoCodecs
  }

  try {
    const payload = native.getHardwareVideoCodecs()
    if (typeof payload !== 'string' || payload.length > 1_024) {
      cachedHardwareVideoCodecs = []
      return cachedHardwareVideoCodecs
    }

    const parsed = JSON.parse(payload) as unknown
    if (!Array.isArray(parsed) || parsed.length > 16) {
      cachedHardwareVideoCodecs = []
      return cachedHardwareVideoCodecs
    }

    cachedHardwareVideoCodecs = [...new Set(parsed
      .filter((value): value is string => typeof value === 'string' && value.length <= 16)
      .map((value) => value.trim().toLocaleLowerCase())
      .filter((value) => supportedHardwareVideoCodecs.has(value)))]
    return cachedHardwareVideoCodecs
  } catch {
    cachedHardwareVideoCodecs = []
    return cachedHardwareVideoCodecs
  }
}

export function postNativeMessage(message: Record<string, unknown>) {
  try {
    window.RayNeoGlasses?.postMessage(JSON.stringify(message))
    return Boolean(window.RayNeoGlasses)
  } catch {
    return false
  }
}
