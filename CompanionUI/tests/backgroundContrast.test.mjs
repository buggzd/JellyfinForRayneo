import assert from 'node:assert/strict'
import test from 'node:test'
import { DEFAULT_BACKGROUND_LAYOUT as defaults, normalizeBackgroundLayout, validBackgroundLayout, centeredBackgroundLayout } from '../src/backgroundLayout.mjs'
import { contrastRatio, luminance, wallpaperViewportCrop, wallpaperTextAppearance } from '../src/backgroundContrast.mjs'

const layout = { ...defaults, transparency: 0 }
const whole = [{ x: 0, y: 0, width: 1, height: 1 }]
function raster(pixel) {
  const width = 48, height = 48
  const data = new Uint8ClampedArray(width * height * 4)
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) data.set([...pixel(x / width, y / height), 255], (y * width + x) * 4)
  return { width, height, data }
}

test('chooses white on a dark photo and dark ink on a light photo', () => {
  assert.equal(contrastRatio(luminance([0, 0, 0]), luminance([255, 255, 255])), 21)
  assert.deepEqual(wallpaperTextAppearance(raster(() => [0, 0, 0]), layout, 1, whole), { tone: 'light', support: false })
  assert.deepEqual(wallpaperTextAppearance(raster(() => [255, 255, 255]), layout, 1, whole), { tone: 'dark', support: false })
})

test('separate text regions choose different colors on one mixed wallpaper', () => {
  const mixed = raster((x, y) => y < 0.5 ? [12, 18, 26] : [240, 235, 228])
  assert.equal(wallpaperTextAppearance(mixed, layout, 1, [{ x: 0, y: 0, width: 1, height: 0.4 }]).tone, 'light')
  assert.equal(wallpaperTextAppearance(mixed, layout, 1, [{ x: 0, y: 0.6, width: 1, height: 0.4 }]).tone, 'dark')
})

test('opacity and the actual crop position affect contrast instead of the original average', () => {
  const mixed = raster((x) => x < 0.5 ? [0, 0, 0] : [255, 255, 255])
  assert.equal(wallpaperTextAppearance(mixed, { ...layout, zoom: 200, x: 0 }, 1, whole).tone, 'light')
  assert.equal(wallpaperTextAppearance(mixed, { ...layout, zoom: 200, x: 1000 }, 1, whole).tone, 'dark')
  const black = raster(() => [0, 0, 0])
  assert.equal(wallpaperTextAppearance(black, { ...layout, transparency: 65 }, 1, whole).tone, 'dark')
  assert.equal(wallpaperTextAppearance(black, { ...layout, transparency: 100 }, 1, whole).tone, 'dark')
  assert.equal(wallpaperTextAppearance(black, layout, 1, whole, 0.9).tone, 'dark')
})

test('sampling follows the SVG centered cover and preserves source aspect after downsampling', () => {
  assert.deepEqual(wallpaperViewportCrop(1200, 900, { ...layout, ratio: 'original' }, 0.5), { x: 375, y: 0, width: 450, height: 900 })
  const mixed = { ...raster((x) => x > 0.5 ? [255, 255, 255] : [0, 0, 0]), sourceWidth: 2400, sourceHeight: 1000 }
  assert.equal(wallpaperTextAppearance(mixed, { ...layout, x: 1000 }, 0.5, whole).tone, 'dark')
  assert.equal(wallpaperTextAppearance(mixed, { ...layout, x: 0 }, 0.5, whole).tone, 'light')
})

test('manual colors win and mixed/failed samples request stronger text support', () => {
  assert.deepEqual(wallpaperTextAppearance(raster(() => [255, 255, 255]), { ...layout, textColor: 'light' }, 1, whole), { tone: 'light', support: true })
  assert.deepEqual(wallpaperTextAppearance(null, { ...layout, textColor: 'dark' }, 1, whole), { tone: 'dark', support: true })
  const mixed = raster((x) => x < 0.5 ? [0, 0, 0] : [255, 255, 255])
  assert.equal(wallpaperTextAppearance(mixed, layout, 1, whole).support, true)
})

test('old crop records migrate to auto, and explicit color survives replacement', () => {
  const { textColor, ...old } = { ...layout, zoom: 150, x: 320 }
  assert.deepEqual(normalizeBackgroundLayout(old), { ...old, textColor: 'auto' })
  for (const value of ['red', '', 0, null, 'LIGHT']) assert.equal(validBackgroundLayout({ ...layout, textColor: value }), false)
  assert.equal(centeredBackgroundLayout({ ...layout, textColor: 'light' }).textColor, 'light')
})

test('near the crossover, small brightness changes do not flip the previous text color', () => {
  const gray = raster(() => [125, 125, 125])
  assert.equal(wallpaperTextAppearance(gray, layout, 1, whole, 0, 'dark').tone, 'dark')
  assert.equal(wallpaperTextAppearance(gray, layout, 1, whole, 0, 'light').tone, 'light')
})
