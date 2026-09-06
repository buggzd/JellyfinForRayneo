import { CircleAlert, Check, Info, LoaderCircle } from 'lucide-react'
import { useEffect, useState } from 'react'

export type FeedbackTone = 'success' | 'error' | 'info'
export type ToastMessage = { text: string; tone: FeedbackTone }

// Retain only passive feedback during its exit; navigation and focus never wait.
export function usePresence(visible: boolean, exitMs = 180) {
  const [retained, setRetained] = useState(visible)
  useEffect(() => {
    if (visible) {
      setRetained(true)
      return
    }
    const timer = window.setTimeout(() => setRetained(false), exitMs)
    return () => window.clearTimeout(timer)
  }, [exitMs, visible])
  return visible || retained
}

export function Toast({ message }: { message: ToastMessage | null }) {
  const [lastMessage, setLastMessage] = useState(message)
  const mounted = usePresence(Boolean(message))
  useEffect(() => {
    if (message) setLastMessage(message)
  }, [message])
  const current = message ?? lastMessage
  if (!mounted || !current) return null
  const Icon = current.tone === 'error' ? CircleAlert : current.tone === 'success' ? Check : Info
  return (
    <div className={`toast toast--${current.tone}${message ? '' : ' is-leaving'}`}
      role="status" aria-atomic="true" aria-hidden={!message}>
      <span className="toast__icon" aria-hidden="true"><Icon size={18} /></span>
      {current.text}
    </div>
  )
}

export function LoadingCards({ label, rail = false }: { label: string; rail?: boolean }) {
  return (
    <div className="loading-cards" role="status" aria-label={label}>
      <div className="loading-cards__label"><LoaderCircle className="is-spinning" size={18} />{label}</div>
      <div className={`loading-cards__grid ${rail ? 'episode-rail' : 'media-grid'}`} aria-hidden="true">
        {Array.from({ length: rail ? 3 : 5 }, (_, index) => (
          <div className={`loading-card${rail ? ' episode-card' : ''}`} key={index}>
            <div className={`loading-card__art${rail ? ' loading-card__art--wide' : ''}`} />
            <i /><i />
          </div>
        ))}
      </div>
    </div>
  )
}
