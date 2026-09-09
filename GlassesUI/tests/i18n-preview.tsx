import React, { useState } from 'react'
import { createRoot } from 'react-dom/client'
import GlassesSettings from '../src/GlassesSettings'
import RemoteTutorial from '../src/RemoteTutorial'
import { useLanguage } from '../src/useLanguage'
import { applyLanguage } from '../../SharedUI/i18n.mjs'
import { applyUiTheme, type UiTheme } from '../../SharedUI/theme.mjs'
import type { SubtitleSize } from '../../SharedUI/subtitles.mjs'
import '../src/styles.css'
import '../../SharedUI/simpleUI.css'
import '../src/simpleUI.css'

// Server-free visual QA of real settings/tutorial components, never credentials.
applyLanguage('en')
function Preview() {
  useLanguage()
  const [theme, setTheme] = useState<UiTheme>('liquid-glass')
  const [size, setSize] = useState<SubtitleSize>('normal')
  const [tutorial, setTutorial] = useState(false)
  return <>
    <nav style={{ position: 'fixed', top: 8, right: 16, zIndex: 10000, display: 'flex', gap: 12, background: '#18222c', padding: 10 }}>
      <button onClick={() => applyLanguage('zh-CN')}>简体中文</button>
      <button onClick={() => applyLanguage('en')}>English</button>
      <button onClick={() => { setTutorial(!tutorial); window.scrollTo(0, 0) }}>Settings / Tutorial</button>
    </nav>
    {tutorial ? <RemoteTutorial onExit={() => setTutorial(false)} simpleUi={theme === 'simpleUI'} />
      : <main style={{ padding: '64px 5vw' }}><GlassesSettings theme={theme} subtitleSize={size}
          onThemeChange={value => { applyUiTheme(value); setTheme(value) }} onSubtitleSizeChange={setSize}
          onLanguageChange={applyLanguage} /></main>}
  </>
}
createRoot(document.getElementById('root')!).render(<Preview />)
