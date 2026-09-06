import React, { useEffect, useState } from 'react'
import { Check, CircleAlert, Info } from 'lucide-react'

export function usePresence(visible, exitMs = 180) {
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

export function Toast({ message }) {
  const [lastMessage, setLastMessage] = useState(message)
  const mounted = usePresence(Boolean(message))
  useEffect(() => {
    if (message) setLastMessage(message)
  }, [message])
  const current = message || lastMessage
  if (!mounted || !current) return null
  const Icon = current.tone === 'error' ? CircleAlert : current.tone === 'success' ? Check : Info
  return (
    <div className={`toast toast--${current.tone}${message ? ' is-visible' : ' is-leaving'}`}
      role="status" aria-atomic="true" aria-hidden={!message}>
      <Icon size={17} aria-hidden="true" />
      <span>{current.text}</span>
    </div>
  )
}
