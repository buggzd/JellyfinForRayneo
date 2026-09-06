const PREVIEW_KEY = 'jellyfin-rayneo-preview-touchpad-background'

export function isTouchpadBackground(value) {
  return value === 'texture' || value === 'black'
}

export function normalizeTouchpadBackground(value, theme) {
  return isTouchpadBackground(value) ? value : theme === 'simpleUI' ? 'black' : 'texture'
}

// An unset choice follows the theme's original default. An explicit choice
// survives theme changes. Native Android owns its own persisted preference.
export function readPreviewTouchpadBackground() {
  try {
    const value = window.localStorage.getItem(PREVIEW_KEY)
    return isTouchpadBackground(value) ? value : null
  } catch {
    return null
  }
}

export function savePreviewTouchpadBackground(value) {
  if (!isTouchpadBackground(value)) return
  try {
    window.localStorage.setItem(PREVIEW_KEY, value)
  } catch { /* The in-memory choice still works when storage is unavailable. */ }
}
