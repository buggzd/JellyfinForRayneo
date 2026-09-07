import assert from 'node:assert/strict'
import test from 'node:test'
import { DEFAULT_BACKGROUND_LAYOUT as defaults, BACKGROUND_RATIOS, validBackgroundLayout, normalizeBackgroundLayout, centeredBackgroundLayout, backgroundCrop, dragBackgroundCrop } from '../src/backgroundLayout.mjs'

test('rejects malformed preferences and restores the former soft centered appearance', () => {
  for (const value of [null, {}, { ...defaults, url: 'https://example.test' }, { ...defaults, transparency: '50' }, { ...defaults, zoom: 99 }, { ...defaults, x: Infinity }, { ...defaults, y: -1 }, { ...defaults, ratio: 'other' }]) {
    assert.equal(validBackgroundLayout(value), false)
    assert.deepEqual(normalizeBackgroundLayout(value), defaults)
  }
  assert.equal(validBackgroundLayout({ ...defaults, transparency: 0, x: 0, y: 1000, zoom: 300 }), true)
})

test('a portrait crop of a landscape image can align with either image edge', () => {
  const crop = backgroundCrop(1600, 900, { ...defaults, ratio: '9:16', x: 0 }, 1 / 2)
  assert.deepEqual(crop, { x: 0, y: 0, width: 506.25, height: 900 })
  const right = backgroundCrop(1600, 900, { ...defaults, ratio: '9:16', x: 1000 }, 1 / 2)
  assert.equal(right.x + right.width, 1600)
  assert.equal(right.height, 900)
})

test('zoom crops inward and lets tall photos reach top and bottom without empty pixels', () => {
  const crop = backgroundCrop(900, 1600, { ...defaults, ratio: '1:1', zoom: 200, x: 1000, y: 1000 }, 1 / 2)
  assert.deepEqual(crop, { x: 450, y: 1150, width: 450, height: 450 })
  assert.deepEqual(backgroundCrop(900, 1600, { ...defaults, ratio: 'original' }, 1 / 2), { x: 0, y: 0, width: 900, height: 1600 })
})

test('every crop preset stays inside portrait, landscape, square and thin sources at all extremes', () => {
  for (const [width, height] of [[1600, 900], [900, 1600], [1000, 1000], [1, 1600], [1600, 1]]) {
    for (const { value: ratio } of BACKGROUND_RATIOS) for (const zoom of [100, 200, 300]) for (const x of [0, 500, 1000]) for (const y of [0, 500, 1000]) {
      const crop = backgroundCrop(width, height, { ...defaults, ratio, zoom, x, y }, 393 / 852)
      assert.ok(crop.x >= 0 && crop.y >= 0 && crop.width > 0 && crop.height > 0)
      assert.ok(crop.x + crop.width <= width + 1e-8 && crop.y + crop.height <= height + 1e-8)
    }
  }
  assert.equal(backgroundCrop(0, 900, defaults, 0.5), null)
})

test('drag follows the finger, clamps at image bounds and leaves uncroppable axes centered', () => {
  const layout = { ...defaults, ratio: '1:1' }
  const image = { width: 1600, height: 900 }
  const crop = backgroundCrop(image.width, image.height, layout, 0.5)
  const frame = { width: 300, height: 300 }
  const moved = dragBackgroundCrop(layout, crop, image, frame, 50, 80)
  assert.equal(moved.x, 286)
  assert.equal(moved.y, 500)
  assert.equal(dragBackgroundCrop(layout, crop, image, frame, 9999, 0).x, 0)
  assert.equal(dragBackgroundCrop(layout, crop, image, frame, -9999, 0).x, 1000)
})

test('replacing a photo recenters the crop while preserving chosen transparency', () => {
  assert.deepEqual(centeredBackgroundLayout({ ...defaults, transparency: 22, zoom: 300, x: 1000, ratio: '1:1' }), { ...defaults, transparency: 22 })
})
