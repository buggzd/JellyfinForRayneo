import { t } from '../../SharedUI/i18n.mjs'
import { useEffect, useState } from 'react'
import './systemClock.css'

function localTime() {
  const now = new Date()
  return `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
}

export default function SystemClock({ active = true, overlay = false }: { active?: boolean; overlay?: boolean }) {
  const [time, setTime] = useState(localTime)

  useEffect(() => {
    if (!active) return
    let timer: number | undefined
    const refresh = () => {
      window.clearTimeout(timer)
      if (document.hidden) return
      setTime(localTime())
      // Follow the device clock at minute boundaries, including after resume.
      timer = window.setTimeout(refresh, 60_000 - Date.now() % 60_000)
    }
    refresh()
    document.addEventListener('visibilitychange', refresh)
    window.addEventListener('focus', refresh)
    return () => {
      window.clearTimeout(timer)
      document.removeEventListener('visibilitychange', refresh)
      window.removeEventListener('focus', refresh)
    }
  }, [active])

  return <time className={`system-clock${overlay ? ' system-clock--overlay' : ''}`} dateTime={time} aria-label={t("当前时间 {0}", { 0: time })} aria-live="off">{time}</time>
}
