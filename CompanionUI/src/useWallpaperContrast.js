import { useEffect } from 'react'
import { wallpaperTextAppearance } from './backgroundContrast.mjs'

function textRegions(element, viewport) {
  const regions = []
  const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT)
  const range = document.createRange()
  while (walker.nextNode()) {
    if (!walker.currentNode.textContent.trim()) continue
    range.selectNodeContents(walker.currentNode)
    for (const rect of range.getClientRects()) {
      if (rect.width && rect.height) regions.push({ x: (rect.x - viewport.x) / viewport.width,
        y: (rect.y - viewport.y) / viewport.height, width: rect.width / viewport.width, height: rect.height / viewport.height })
    }
  }
  if (!regions.length) {
    const rect = element.getBoundingClientRect()
    if (rect.width && rect.height) regions.push({ x: (rect.x - viewport.x) / viewport.width,
      y: (rect.y - viewport.y) / viewport.height, width: rect.width / viewport.width, height: rect.height / viewport.height })
  }
  return regions
}

function navigationRegions(element, viewport, regions) {
  const nav = element.closest('.bottom-nav')
  return regions.map((region) => {
    const x = viewport.x + (region.x + region.width / 2) * viewport.width
    const y = viewport.y + (region.y + region.height / 2) * viewport.height
    const underneath = document.elementsFromPoint(x, y).find((node) => !nav.contains(node))
    // Scrolling content can sit between the fixed wallpaper and clear navigation.
    // These are the light surfaces defined in wallpaperContrast.css.
    const panel = underneath?.closest('.settings-group__body, .settings-account, .account-server-card, .accounts-empty, .server-card, .restore-card, .field, .quick-code, .manual-card, .scan-empty, .saved-accounts-link')
    const clearCard = underneath?.closest('.device-hero, .connection-card')
    const behind = panel ? 0.92 : clearCard ? 0.12 : 0
    return { ...region, surface: 1 - (1 - 0.12) * (1 - behind) }
  })
}

export function useWallpaperContrast(rootRef, background, layout, screenAspect, enabled, viewportSelector) {
  const { transparency, ratio, zoom, x, y, textColor } = layout
  useEffect(() => {
    const root = rootRef.current
    if (!root || !enabled) return undefined
    let frame = 0
    const painted = new Set()
    const update = () => {
      frame = 0
      for (const element of painted) if (!element.isConnected) painted.delete(element)
      const viewport = (viewportSelector ? root.querySelector(viewportSelector) : root)?.getBoundingClientRect()
      if (!viewport?.width || !viewport.height) return
      for (const element of root.querySelectorAll('[data-wallpaper-text]')) {
        // The editor has its own crop and scope; the saved wallpaper must not override it.
        if (element.closest('[data-wallpaper-scope]') !== root) continue
        let regions = textRegions(element, viewport)
        if (!regions.length) continue
        if (element.dataset.wallpaperText === 'navigation') regions = navigationRegions(element, viewport, regions)
        const appearance = wallpaperTextAppearance(background.samples, { transparency, ratio, zoom, x, y, textColor }, screenAspect,
          regions, element.dataset.wallpaperText === 'glass' ? 0.12 : 0, element.dataset.textTone)
        if (element.dataset.textTone !== appearance.tone) element.dataset.textTone = appearance.tone
        if (element.dataset.textSupport !== String(appearance.support)) element.dataset.textSupport = String(appearance.support)
        painted.add(element)
      }
    }
    const schedule = () => { if (!frame) frame = requestAnimationFrame(update) }
    const resize = new ResizeObserver(schedule)
    resize.observe(root)
    const mutation = new MutationObserver(schedule)
    mutation.observe(root, { childList: true, subtree: true })
    window.addEventListener('scroll', schedule, { passive: true, capture: true })
    window.addEventListener('resize', schedule)
    root.addEventListener('transitionend', schedule)
    root.addEventListener('toggle', schedule, true)
    schedule()
    return () => {
      cancelAnimationFrame(frame)
      resize.disconnect()
      mutation.disconnect()
      window.removeEventListener('scroll', schedule, true)
      window.removeEventListener('resize', schedule)
      root.removeEventListener('transitionend', schedule)
      root.removeEventListener('toggle', schedule, true)
      for (const element of painted) {
        delete element.dataset.textTone
        delete element.dataset.textSupport
      }
    }
  }, [rootRef, background.samples, background.url, transparency, ratio, zoom, x, y, textColor, screenAspect, enabled, viewportSelector])
}
