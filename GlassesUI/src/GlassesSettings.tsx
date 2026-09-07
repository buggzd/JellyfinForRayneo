import { Check, Palette, Subtitles, Volume2 } from 'lucide-react'
import { useState } from 'react'
import { uiSounds } from './uiSounds'
import type { UiTheme } from '../../SharedUI/theme.mjs'
import { SUBTITLE_SIZES, subtitleFontSize, type SubtitleSize } from '../../SharedUI/subtitles.mjs'
import './glassesSettings.css'

function SubtitleSizeOptions({ value, onChange }: {
  value: SubtitleSize
  onChange: (size: SubtitleSize) => void
}) {
  return (
    <div className="subtitle-size-options" role="group" aria-label="字幕大小">
      {SUBTITLE_SIZES.map(option => (
        <button key={option.value} type="button" data-focusable="true"
          className={`subtitle-size-option${value === option.value ? ' is-active' : ''}`}
          aria-pressed={value === option.value} aria-label={`字幕大小：${option.label}`}
          onClick={() => onChange(option.value)}>
          {option.label}<Check size={16} aria-hidden="true" />
        </button>
      ))}
    </div>
  )
}

export default function GlassesSettings({ theme, subtitleSize, onThemeChange, onSubtitleSizeChange }: {
  theme: UiTheme
  subtitleSize: SubtitleSize
  onThemeChange: (theme: UiTheme) => void
  onSubtitleSizeChange: (size: SubtitleSize) => void
}) {
  const [soundsEnabled, setSoundsEnabled] = useState(uiSounds.isEnabled)
  const [soundSaveFailed, setSoundSaveFailed] = useState(false)
  const toggleSounds = () => {
    const enabled = !soundsEnabled
    setSoundSaveFailed(!uiSounds.setEnabled(enabled))
    setSoundsEnabled(enabled)
    if (enabled) uiSounds.play('toggle-on')
  }
  return (
    <>
      <header className="glasses-settings-heading">
        <small>PREFERENCES</small><h1>按你的习惯观看</h1>
        <p>单击选择，自动保存。</p>
      </header>
      <div className="glasses-settings-grid">
        <section className="glasses-setting glass-panel" aria-labelledby="glasses-theme-heading">
          <header><Palette size={24} /><div><h2 id="glasses-theme-heading">界面风格</h2><p>与手机同步，随时切换</p></div></header>
          <div className="glasses-theme-options" role="group" aria-label="界面风格">
            {([{ value: 'liquid-glass', label: '液态玻璃', description: '通透层次，柔和光感' },
              { value: 'simpleUI', label: 'simpleUI', description: '清晰表面，轻量呈现' }] as const).map(option => (
              <button key={option.value} type="button" data-focusable="true"
                data-autofocus={theme === option.value ? 'true' : undefined}
                className={`glasses-theme-option${theme === option.value ? ' is-active' : ''}`}
                aria-pressed={theme === option.value} onClick={() => onThemeChange(option.value)}>
                <span className={`glasses-theme-sample glasses-theme-sample--${option.value}`} aria-hidden="true"><i /><b /><b /><b /></span>
                <span className="glasses-theme-copy"><strong>{option.label}</strong><small>{option.description}</small></span>
                <Check className="glasses-theme-check" size={19} aria-hidden="true" />
              </button>
            ))}
          </div>
        </section>
        <section className="glasses-setting glass-panel" aria-labelledby="glasses-subtitle-heading">
          <header><Subtitles size={24} /><div><h2 id="glasses-subtitle-heading">字幕大小</h2><p>先看看哪个字号更舒服</p></div></header>
          <div className="subtitle-preview" aria-label="字幕效果预览">
            <span>字幕预览</span>
            <p style={{ fontSize: subtitleFontSize(subtitleSize) }}>每一帧，都值得看清。<br />Every frame tells a story.</p>
          </div>
          <SubtitleSizeOptions value={subtitleSize} onChange={onSubtitleSizeChange} />
          <p className="subtitle-size-note">选择后应用于播放字幕。已固定在视频画面里的字幕不受此设置影响。</p>
        </section>
        <section className="glasses-setting glasses-sound-setting glass-panel" aria-labelledby="glasses-sound-heading">
          <header><Volume2 size={24} /><div><h2 id="glasses-sound-heading">UI 音效</h2>
            <p id="glasses-sound-description">{soundSaveFailed ? '当前选择已生效，但未能保存，重启后请重新设置。' : '眼镜端操作提示音，不影响影片声音。'}</p>
          </div></header>
          <button type="button" className="glasses-sound-toggle" role="switch" data-focusable="true"
            data-ui-sound="none" aria-checked={soundsEnabled} aria-labelledby="glasses-sound-heading"
            aria-describedby="glasses-sound-description" onClick={toggleSounds}>
            <span>{soundsEnabled ? '已开启' : '已关闭'}</span><i aria-hidden="true"><b /></i>
          </button>
        </section>
      </div>
    </>
  )
}
