import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.jsx'
import './styles.css'

async function start() {
  if (import.meta.env.DEV) {
    const { installDevelopmentBridge } = await import('./developmentBridge.js')
    installDevelopmentBridge()
  }

  ReactDOM.createRoot(document.getElementById('root')).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>,
  )
}

void start()
