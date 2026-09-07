import { backgroundCrop } from './backgroundLayout.mjs'

const BASE = [234, 247, 250]
const INK = { dark: [14, 31, 44], light: [255, 255, 255] }
const INK_LUMINANCE = { dark: luminance(INK.dark), light: luminance(INK.light) }
const clamp = (value, min, max) => Math.min(max, Math.max(min, value))

export function luminance(rgb) {
  const linear = rgb.map((channel) => {
    const value = channel / 255
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4
  })
  return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722
}

export function contrastRatio(a, b) {
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
}

// SVG's final centered cover can be narrower than the chosen crop preset.
export function wallpaperViewportCrop(width, height, layout, aspect) {
  const crop = backgroundCrop(width, height, layout, aspect)
  if (!crop) return null
  const visibleWidth = Math.min(crop.width, crop.height * aspect)
  const visibleHeight = visibleWidth / aspect
  return { x: crop.x + (crop.width - visibleWidth) / 2, y: crop.y + (crop.height - visibleHeight) / 2,
    width: visibleWidth, height: visibleHeight }
}

// One tiny in-memory raster per imported image; no full-image reads while scrolling.
export function sampleBackground(image) {
  const scale = Math.min(1, 96 / Math.max(image.naturalWidth, image.naturalHeight))
  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, Math.round(image.naturalWidth * scale))
  canvas.height = Math.max(1, Math.round(image.naturalHeight * scale))
  const context = canvas.getContext('2d', { willReadFrequently: true })
  context.drawImage(image, 0, 0, canvas.width, canvas.height)
  return { width: canvas.width, height: canvas.height, sourceWidth: image.naturalWidth, sourceHeight: image.naturalHeight,
    data: context.getImageData(0, 0, canvas.width, canvas.height).data }
}

export function wallpaperTextAppearance(samples, layout, aspect, regions, surface = 0, previousTone) {
  const forced = layout.textColor === 'light' || layout.textColor === 'dark' ? layout.textColor : null
  if (!samples) return { tone: forced || 'dark', support: true }
  const width = samples.sourceWidth || samples.width
  const height = samples.sourceHeight || samples.height
  const crop = wallpaperViewportCrop(width, height, layout, aspect)
  if (!crop) return { tone: forced || 'dark', support: true }
  const light = []
  const dark = []
  const opacity = 1 - layout.transparency / 100
  for (const region of regions) {
    for (let row = 0; row < 3; row += 1) for (let column = 0; column < 7; column += 1) {
      const x = clamp(Math.floor((crop.x + (region.x + region.width * (column + 0.5) / 7) * crop.width) / width * samples.width), 0, samples.width - 1)
      const y = clamp(Math.floor((crop.y + (region.y + region.height * (row + 0.5) / 3) * crop.height) / height * samples.height), 0, samples.height - 1)
      const offset = (y * samples.width + x) * 4
      const rgb = BASE.map((base, channel) => (samples.data[offset + channel] * opacity + base * (1 - opacity)) * (1 - surface) + 255 * surface)
      const value = luminance(rgb)
      light.push(contrastRatio(INK_LUMINANCE.light, value))
      dark.push(contrastRatio(INK_LUMINANCE.dark, value))
    }
  }
  if (!light.length) return { tone: forced || 'dark', support: true }
  light.sort((a, b) => a - b)
  dark.sort((a, b) => a - b)
  // The lower contrast quartile protects mixed photos better than average brightness.
  const index = Math.floor(light.length * 0.25)
  const scores = { light: light[index], dark: dark[index] }
  let tone = forced || (scores.light > scores.dark ? 'light' : 'dark')
  if (!forced && (previousTone === 'light' || previousTone === 'dark') && scores[tone] < scores[previousTone] + 0.4) tone = previousTone
  return { tone, support: (tone === 'light' ? light[0] : dark[0]) < 4.5 }
}
