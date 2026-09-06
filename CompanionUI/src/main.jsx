import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.jsx'
import './styles.css'
import './themeSelector.css'
import './settings.css'
import '../../SharedUI/simpleUI.css'
import './simpleUI.css'
import './touchpadBackground.css'
import { applyUiTheme, normalizeUiTheme, readPreviewTheme } from '../../SharedUI/theme.mjs'

async function start() {
  if (import.meta.env.DEV) {
    const { installDevelopmentBridge } = await import('./developmentBridge.js')
    installDevelopmentBridge()
  }

  let theme = readPreviewTheme()
  if (window.JellyfinNative) {
    try { theme = normalizeUiTheme(JSON.parse(window.JellyfinNative.getState()).uiTheme) }
    catch { theme = 'liquid-glass' }
  }
  applyUiTheme(theme)
  if (theme === 'liquid-glass') {
    for (const name of ['luma-global-ice-glass.png', 'liquid-blue.png']) {
      const preload = document.createElement('link')
      preload.rel = 'preload'
      preload.as = 'image'
      preload.href = `${import.meta.env.BASE_URL}art/${name}`
      document.head.append(preload)
    }
  }

  ReactDOM.createRoot(document.getElementById('root')).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>,
  )
}

void start()
