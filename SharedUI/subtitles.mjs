export const DEFAULT_SUBTITLE_SIZE = 'normal'
export const PREVIEW_SUBTITLE_SIZE_KEY = 'jellyfin-rayneo-preview-subtitle-size'
export const SUBTITLE_SIZES = Object.freeze([
  Object.freeze({ value: 'small', label: '小', scale: 0.8 }),
  Object.freeze({ value: 'normal', label: '标准', scale: 1 }),
  Object.freeze({ value: 'large', label: '大', scale: 1.25 }),
  Object.freeze({ value: 'extra-large', label: '特大', scale: 1.5 }),
])

export function isSubtitleSize(value) {
  return SUBTITLE_SIZES.some(option => option.value === value)
}

export function normalizeSubtitleSize(value) {
  return isSubtitleSize(value) ? value : DEFAULT_SUBTITLE_SIZE
}

export function subtitleFontSize(value) {
  const { scale } = SUBTITLE_SIZES.find(option => option.value === normalizeSubtitleSize(value))
  return `clamp(${23 * scale}px, ${1.65 * scale}vw, ${31 * scale}px)`
}

// Preview preferences never contain native account or session data.
export function readPreviewSubtitleSize() {
  try {
    return normalizeSubtitleSize(window.localStorage.getItem(PREVIEW_SUBTITLE_SIZE_KEY))
  } catch {
    return DEFAULT_SUBTITLE_SIZE
  }
}

export function savePreviewSubtitleSize(value) {
  try {
    window.localStorage.setItem(PREVIEW_SUBTITLE_SIZE_KEY, normalizeSubtitleSize(value))
  } catch { /* Keep the current preview usable when storage is unavailable. */ }
}
