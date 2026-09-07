import assert from 'node:assert/strict'
import test from 'node:test'
import { DEFAULT_GLASS_TRANSPARENCY, normalizeGlassTransparency, validGlassTransparency, glassSurfaceOpacity } from '../src/liquidAppearance.mjs'
import { wallpaperTextAppearance } from '../src/backgroundContrast.mjs'
import { DEFAULT_BACKGROUND_LAYOUT } from '../src/backgroundLayout.mjs'

test('glass transparency accepts only integer percentages and safely restores old or corrupt state', () => {
  for (const value of [0, 1, 50, 88, 99, 100]) {
    assert.equal(validGlassTransparency(value), true)
    assert.equal(normalizeGlassTransparency(value), value)
  }
  for (const value of [null, undefined, '', '50', false, {}, [], NaN, Infinity, -1, 101, 50.5]) {
    assert.equal(validGlassTransparency(value), false)
    assert.equal(normalizeGlassTransparency(value), DEFAULT_GLASS_TRANSPARENCY)
  }
})

test('card, nested choice and overlaid navigation compose their actual adjustable white fill', () => {
  assert.equal(glassSurfaceOpacity(0), 1)
  assert.equal(glassSurfaceOpacity(100, 3), 0)
  assert.equal(glassSurfaceOpacity(50, 0), 0)
  assert.equal(glassSurfaceOpacity(50, 1), 0.5)
  assert.equal(glassSurfaceOpacity(50, 2), 0.75)
  assert.equal(glassSurfaceOpacity(50, 3), 0.875)
})

test('text responds to adjustable and stacked glass, including opaque and completely clear extremes', () => {
  const samples = { width: 1, height: 1, data: new Uint8ClampedArray([0, 0, 0, 255]) }
  const layout = { ...DEFAULT_BACKGROUND_LAYOUT, transparency: 0 }
  const regions = [{ x: 0, y: 0, width: 1, height: 1 }]
  const tone = (value, layers = 1) => wallpaperTextAppearance(samples, layout, 1, regions, glassSurfaceOpacity(value, layers)).tone
  assert.equal(tone(100), 'light')
  assert.equal(tone(88), 'light')
  assert.equal(tone(0), 'dark')
  assert.equal(tone(70), 'light')
  assert.equal(tone(70, 2), 'dark')
  assert.deepEqual(wallpaperTextAppearance(samples, { ...layout, textColor: 'light' }, 1, regions, glassSurfaceOpacity(0)), { tone: 'light', support: true })
})
