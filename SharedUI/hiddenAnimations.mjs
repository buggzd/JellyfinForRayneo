// Keep exit fades moving, then suspend only the decorative loops selected by CSS.
// Removing the class restores CSS playback state (including a paused video).
export function suspendHiddenAnimations(element, hidden) {
  if (!element || typeof element.getAnimations !== 'function') return undefined
  const className = 'has-suspended-animations'
  element.classList.remove(className)
  if (!hidden) return undefined

  let cancelled = false
  const suspend = () => {
    if (!cancelled && element.isConnected
      && element.ownerDocument.defaultView.getComputedStyle(element).opacity === '0') {
      element.classList.add(className)
    }
  }
  // getAnimations flushes the updated hidden style and exposes its actual
  // transition duration, including reduced motion and interrupted transitions.
  const exits = element.getAnimations().filter(animation =>
    Number.isFinite(animation.effect?.getComputedTiming().endTime))
  if (exits.length) {
    void Promise.allSettled(exits.map(animation => animation.finished)).then(suspend)
  } else {
    suspend()
  }
  return () => {
    cancelled = true
    element.classList.remove(className)
  }
}
