export const DEFAULT_GLASS_TRANSPARENCY = 88
const PREVIEW_KEY = 'jellyfin-rayneo-preview-glass-transparency'

export function validGlassTransparency(value) {
  return Number.isInteger(value) && value >= 0 && value <= 100
}

export function normalizeGlassTransparency(value) {
  return validGlassTransparency(value) ? value : DEFAULT_GLASS_TRANSPARENCY
}

// White glass layers compose without changing the opacity of their text or icons.
export function glassSurfaceOpacity(transparency, layers = 1) {
  return 1 - (normalizeGlassTransparency(transparency) / 100) ** layers
}

export function liquidSurfaceStyle(transparency) {
  const fill = glassSurfaceOpacity(transparency)
  return { '--liquid-fill': fill, '--liquid-blur': `${2 + fill * 16}px` }
}

export function readPreviewGlassTransparency() {
  try {
    const value = window.localStorage.getItem(PREVIEW_KEY)
    return normalizeGlassTransparency(value === null ? null : JSON.parse(value))
  } catch {
    return DEFAULT_GLASS_TRANSPARENCY
  }
}

export function savePreviewGlassTransparency(value) {
  if (!validGlassTransparency(value)) return
  try {
    window.localStorage.setItem(PREVIEW_KEY, String(value))
  } catch { /* Keep the in-memory appearance when browser storage is unavailable. */ }
}
