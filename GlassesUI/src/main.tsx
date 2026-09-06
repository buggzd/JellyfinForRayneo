import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './styles.css'
import '../../SharedUI/simpleUI.css'
import './simpleUI.css'
import { applyUiTheme, readPreviewTheme } from '../../SharedUI/theme.mjs'

async function start() {
  try {
    applyUiTheme(window.RayNeoGlasses
      ? JSON.parse(window.RayNeoGlasses.getBootstrapState()).uiTheme
      : readPreviewTheme())
  } catch {
    applyUiTheme('liquid-glass')
  }
  const root = createRoot(document.getElementById('root')!)
  if (import.meta.env.DEV) {
    if (new URLSearchParams(window.location.search).has('tutorial')) {
      const { default: TutorialPreview } = await import('./TutorialPreview')
      root.render(<StrictMode><TutorialPreview /></StrictMode>)
      return
    }
    const { installDevelopmentBridge } = await import('./developmentBridge')
    installDevelopmentBridge()
  }

  root.render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

void start()
