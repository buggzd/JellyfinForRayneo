export const DEFAULT_BACKGROUND_LAYOUT = Object.freeze({ transparency: 65, ratio: 'screen', zoom: 100, x: 500, y: 500 })
export const BACKGROUND_RATIOS = Object.freeze([
  { value: 'screen', label: '屏幕' },
  { value: '9:16', label: '9:16' },
  { value: '3:4', label: '3:4' },
  { value: '1:1', label: '1:1' },
  { value: 'original', label: '原图' },
])
const integer = (value, min, max) => Number.isInteger(value) && value >= min && value <= max
const clamp = (value, min, max) => Math.min(max, Math.max(min, value))

export function validBackgroundLayout(value) {
  return value !== null && typeof value === 'object' && Object.keys(value).length === 5
    && integer(value.transparency, 0, 100) && integer(value.zoom, 100, 300)
    && integer(value.x, 0, 1000) && integer(value.y, 0, 1000)
    && BACKGROUND_RATIOS.some(({ value: ratio }) => ratio === value.ratio)
}

export function normalizeBackgroundLayout(value) {
  return { ...(validBackgroundLayout(value) ? value : DEFAULT_BACKGROUND_LAYOUT) }
}

export function sameBackgroundLayout(a, b) {
  return validBackgroundLayout(a) && validBackgroundLayout(b)
    && Object.keys(DEFAULT_BACKGROUND_LAYOUT).every((key) => a[key] === b[key])
}

export function centeredBackgroundLayout(value) {
  return { ...DEFAULT_BACKGROUND_LAYOUT, transparency: normalizeBackgroundLayout(value).transparency }
}

// Keep the crop in source-image coordinates. Rendering and editing share this
// rectangle, so changing the crop never destroys the private source image.
export function backgroundCrop(width, height, value, screenAspect) {
  if (![width, height, screenAspect].every((n) => Number.isFinite(n) && n > 0)) return null
  const layout = normalizeBackgroundLayout(value)
  const aspect = layout.ratio === 'screen' ? screenAspect : layout.ratio === 'original' ? width / height
    : { '9:16': 9 / 16, '3:4': 3 / 4, '1:1': 1 }[layout.ratio]
  const cropWidth = Math.min(width, height * aspect) * 100 / layout.zoom
  const cropHeight = cropWidth / aspect
  return {
    x: Math.max(0, width - cropWidth) * layout.x / 1000,
    y: Math.max(0, height - cropHeight) * layout.y / 1000,
    width: cropWidth,
    height: cropHeight,
  }
}

export function dragBackgroundCrop(layout, crop, image, frame, dx, dy) {
  const move = (position, distance, cropSize, imageSize, frameSize) => imageSize - cropSize < 0.0001
    ? position : clamp(Math.round(position - distance * cropSize / frameSize / (imageSize - cropSize) * 1000), 0, 1000)
  return { ...layout,
    x: move(layout.x, dx, crop.width, image.width, frame.width),
    y: move(layout.y, dy, crop.height, image.height, frame.height),
  }
}
