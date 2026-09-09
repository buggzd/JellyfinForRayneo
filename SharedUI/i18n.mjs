import en from './locales/en.json' with { type: 'json' }

// Chinese source messages are stable keys. Never pass server titles or subtitles to t.
export const LANGUAGE_OPTIONS = Object.freeze(['system', 'zh-CN', 'en'])
export const PREVIEW_LANGUAGE_KEY = 'tachi-preview-language'
export function isLanguage(value) { return LANGUAGE_OPTIONS.includes(value) }
export function normalizeLanguage(value) { return isLanguage(value) ? value : 'system' }
export function resolveLanguage(value, systemLanguage = globalThis.navigator?.language ?? 'en') {
  const preference = normalizeLanguage(value)
  return preference === 'system' ? (/^zh(?:-|$)/i.test(systemLanguage) ? 'zh-CN' : 'en') : preference
}
export function readPreviewLanguage() {
  try { return normalizeLanguage(globalThis.localStorage?.getItem(PREVIEW_LANGUAGE_KEY)) }
  catch { return 'system' }
}
export function savePreviewLanguage(value) {
  try { globalThis.localStorage?.setItem(PREVIEW_LANGUAGE_KEY, normalizeLanguage(value)) }
  catch { /* The current page can still change language without storage. */ }
}
let preference = readPreviewLanguage()
let systemLanguage = globalThis.navigator?.language ?? 'en'
let locale = resolveLanguage(preference, systemLanguage)
const listeners = new Set()
export const getLanguage = () => preference
export const getLocale = () => locale
export const subscribeLanguage = listener => { listeners.add(listener); return () => listeners.delete(listener) }
export function applyLanguage(value, system = globalThis.navigator?.language ?? 'en') {
  const next = normalizeLanguage(value)
  const resolved = resolveLanguage(next, system)
  const changed = next !== preference || resolved !== locale
  preference = next
  systemLanguage = system
  locale = resolved
  if (globalThis.document) {
    document.documentElement.lang = locale
    document.title = locale === 'zh-CN' ? 'tachi（塔奇）' : 'tachi'
  }
  if (changed) listeners.forEach(listener => listener())
}
export function t(key, values = {}) {
  const message = locale === 'en' && Object.hasOwn(en, key) ? en[key] : key
  return message.replace(/\{(\w+)\}/g, (token, name) => Object.hasOwn(values, name) ? String(values[name]) : token)
}
// Native publishes only bounded, fixed diagnostic messages. These two legacy
// messages contain numbers; do not pattern-match or translate arbitrary content.
export function nativeMessage(message = '') {
  const discovery = /^发现 (\d+) 台 Jellyfin 服务器。$/.exec(message)
  if (discovery) return t('发现 {0} 台 Jellyfin 服务器。', { 0: discovery[1] })
  const http = /^Jellyfin 请求失败（HTTP (\d{3})），请检查服务器。$/.exec(message)
  if (http) return t('Jellyfin 请求失败（HTTP {0}），请检查服务器。', { 0: http[1] })
  return t(message)
}
if (globalThis.addEventListener) {
  globalThis.addEventListener('languagechange', () => applyLanguage(preference))
  globalThis.addEventListener('storage', event => {
    if (globalThis.window?.RayNeoGlasses || globalThis.window?.JellyfinNative) return
    if (event.key === PREVIEW_LANGUAGE_KEY) applyLanguage(readPreviewLanguage(), systemLanguage)
  })
}
