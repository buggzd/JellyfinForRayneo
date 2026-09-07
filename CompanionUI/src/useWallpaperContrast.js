import { useEffect } from 'react'
import { wallpaperTextAppearance } from './backgroundContrast.mjs'
import { DEFAULT_GLASS_TRANSPARENCY, glassSurfaceOpacity } from './liquidAppearance.mjs'

function surfaceBehind(element, transparency) {
  let layers = 0
  for (let node = element; node; node = node.parentElement) {
    if (node.hasAttribute('data-liquid-surface')) layers += 1
  }
  const glass = glassSurfaceOpacity(transparency, layers)
  // Login/form panels retain a fixed light fill; appearance cards share the slider.
  const form = element?.closest('.server-card, .restore-card, .field, .quick-code, .manual-card, .scan-empty, .saved-accounts-link')
  return form ? 1 - (1 - glass) * (1 - 0.92) : glass
}

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

function navigationRegions(element, viewport, regions, transparency) {
  const nav = element.closest('.bottom-nav')
  return regions.map((region) => {
    const x = viewport.x + (region.x + region.width / 2) * viewport.width
    const y = viewport.y + (region.y + region.height / 2) * viewport.height
    const underneath = document.elementsFromPoint(x, y).find((node) => !nav.contains(node))
    const behind = surfaceBehind(underneath, transparency)
    return { ...region, surface: 1 - (1 - glassSurfaceOpacity(transparency)) * (1 - behind) }
  })
}

export function useWallpaperContrast(rootRef, background, layout, screenAspect, enabled, viewportSelector, glassTransparency = DEFAULT_GLASS_TRANSPARENCY) {
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
        if (element.dataset.wallpaperText === 'navigation') regions = navigationRegions(element, viewport, regions, glassTransparency)
        const appearance = wallpaperTextAppearance(background.samples, { transparency, ratio, zoom, x, y, textColor }, screenAspect,
          regions, surfaceBehind(element, glassTransparency), element.dataset.textTone)
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
  }, [rootRef, background.samples, background.url, transparency, ratio, zoom, x, y, textColor, screenAspect, enabled, viewportSelector, glassTransparency])
}
