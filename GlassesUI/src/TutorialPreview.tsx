import { useLanguage } from './useLanguage'
import { t } from '../../SharedUI/i18n.mjs'
import { useState } from 'react'
import RemoteTutorial from './RemoteTutorial'
import type { TutorialOutcome } from './tutorialState'

// Development-only, server-free entry; never loads credentials or a media player.
export default function TutorialPreview() {
  useLanguage()
  const [outcome, setOutcome] = useState<TutorialOutcome | null>(null)
  if (!outcome) return <RemoteTutorial simpleUi={document.documentElement.dataset.uiTheme === 'simpleUI'} onExit={setOutcome} />
  return (
    <div className="remote-tutorial">
      <main className="tutorial-complete" style={{ height: '100%' }}>
        <span className="tutorial-eyebrow">{t("教学预览已结束")}</span>
        <h1>{outcome === 'completed' ? t("六个动作，全部完成。") : t("随时可以回来练习。")}</h1>
        <p>{t("在眼镜中，结束教学后会回到媒体库界面。")}</p>
        <div className="tutorial-actions" style={{ marginTop: 30 }}>
          <button className="tutorial-button tutorial-button--primary" autoFocus onClick={() => setOutcome(null)}>{t("重新预览")}</button>
        </div>
      </main>
    </div>
  )
}
