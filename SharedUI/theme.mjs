// These exact values are also whitelisted by the Android bridge.
export const DEFAULT_UI_THEME = 'liquid-glass'
export const SIMPLE_UI_THEME = 'simpleUI'
export const PREVIEW_THEME_KEY = 'jellyfin-rayneo-preview-theme'

export function normalizeUiTheme(value) {
  return value === SIMPLE_UI_THEME ? SIMPLE_UI_THEME : DEFAULT_UI_THEME
}

export function applyUiTheme(value) {
  const theme = normalizeUiTheme(value)
  document.documentElement.dataset.uiTheme = theme
  return theme
}

// Browser previews only. Android remains the sole persisted preference source.
export function readPreviewTheme() {
  try {
    return normalizeUiTheme(window.localStorage.getItem(PREVIEW_THEME_KEY))
  } catch {
    return DEFAULT_UI_THEME
  }
}

export function savePreviewTheme(value) {
  try {
    window.localStorage.setItem(PREVIEW_THEME_KEY, normalizeUiTheme(value))
  } catch {
    // A storage-restricted preview can still switch for the current page.
  }
}
