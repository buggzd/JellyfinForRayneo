import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './styles.css'

async function start() {
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
