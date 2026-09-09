import { getLanguage, type Language, t } from '../../SharedUI/i18n.mjs'
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
    <div className="subtitle-size-options" role="group" aria-label={t("字幕大小")}>
      {SUBTITLE_SIZES.map(option => (
        <button key={option.value} type="button" data-focusable="true"
          className={`subtitle-size-option${value === option.value ? ' is-active' : ''}`}
          aria-pressed={value === option.value} aria-label={t("字幕大小：{0}", { 0: t(option.label) })}
          onClick={() => onChange(option.value)}>
          {t(option.label)}<Check size={16} aria-hidden="true" />
        </button>
      ))}
    </div>
  )
}

export default function GlassesSettings({ theme, subtitleSize, onThemeChange, onSubtitleSizeChange, onLanguageChange }: {
  onLanguageChange: (language: Language) => void
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
        <small>PREFERENCES</small><h1>{t("按你的习惯观看")}</h1>
        <p>{t("单击选择，自动保存。")}</p>
      </header>
      <div className="glasses-settings-grid">
        <section className="glasses-setting glasses-language-setting glass-panel">
          <header><div><h2>{t('语言')}</h2><p>{t('两端同步，自动保存。')}</p></div></header>
          <div className="subtitle-size-options language-options" role="group" aria-label={t('语言')}>
            {(['system', 'zh-CN', 'en'] as const).map(language => <button key={language} type="button" data-focusable="true"
              className={`subtitle-size-option${getLanguage() === language ? ' is-active' : ''}`}
              aria-pressed={getLanguage() === language} onClick={() => onLanguageChange(language)}>
              {language === 'system' ? t('跟随系统') : language === 'zh-CN' ? '简体中文' : 'English'}
            </button>)}
          </div>
        </section>
        <section className="glasses-setting glass-panel" aria-labelledby="glasses-theme-heading">
          <header><Palette size={24} /><div><h2 id="glasses-theme-heading">{t("界面风格")}</h2><p>{t("与手机同步，随时切换")}</p></div></header>
          <div className="glasses-theme-options" role="group" aria-label={t("界面风格")}>
            {([{ value: 'liquid-glass', get label() { return t("液态玻璃") }, get description() { return t("通透层次，柔和光感") } },
              { value: 'simpleUI', label: 'simpleUI', get description() { return t("清晰表面，轻量呈现") } }] as const).map(option => (
              <button key={option.value} type="button" data-focusable="true"
                data-autofocus={theme === option.value ? 'true' : undefined}
                className={`glasses-theme-option${theme === option.value ? ' is-active' : ''}`}
                aria-pressed={theme === option.value} onClick={() => onThemeChange(option.value)}>
                <span className={`glasses-theme-sample glasses-theme-sample--${option.value}`} aria-hidden="true"><i /><b /><b /><b /></span>
                <span className="glasses-theme-copy"><strong>{t(option.label)}</strong><small>{option.description}</small></span>
                <Check className="glasses-theme-check" size={19} aria-hidden="true" />
              </button>
            ))}
          </div>
        </section>
        <section className="glasses-setting glass-panel" aria-labelledby="glasses-subtitle-heading">
          <header><Subtitles size={24} /><div><h2 id="glasses-subtitle-heading">{t("字幕大小")}</h2><p>{t("先看看哪个字号更舒服")}</p></div></header>
          <div className="subtitle-preview" aria-label={t("字幕效果预览")}>
            <span>{t("字幕预览")}</span>
            <p style={{ fontSize: subtitleFontSize(subtitleSize) }}>{t("每一帧，都值得看清。")}<br />Every frame tells a story.</p>
          </div>
          <SubtitleSizeOptions value={subtitleSize} onChange={onSubtitleSizeChange} />
          <p className="subtitle-size-note">{t("应用于普通文字字幕。ASS/SSA 使用字幕原有字号和排版；已烧录到视频的字幕不受此设置影响。")}</p>
        </section>
        <section className="glasses-setting glasses-sound-setting glass-panel" aria-labelledby="glasses-sound-heading">
          <header><Volume2 size={24} /><div><h2 id="glasses-sound-heading">{t("UI 音效")}</h2>
            <p id="glasses-sound-description">{soundSaveFailed ? t("当前选择已生效，但未能保存，重启后请重新设置。") : t("眼镜端操作提示音，不影响影片声音。")}</p>
          </div></header>
          <button type="button" className="glasses-sound-toggle" role="switch" data-focusable="true"
            data-ui-sound="none" aria-checked={soundsEnabled} aria-labelledby="glasses-sound-heading"
            aria-describedby="glasses-sound-description" onClick={toggleSounds}>
            <span>{soundsEnabled ? t("已开启") : t("已关闭")}</span><i aria-hidden="true"><b /></i>
          </button>
        </section>
      </div>
    </>
  )
}
