import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './styles.css'

async function start() {
  if (import.meta.env.DEV) {
    const { installDevelopmentBridge } = await import('./developmentBridge')
    installDevelopmentBridge()
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

void start()
