import workerSource from 'libass-wasm/dist/js/subtitles-octopus-worker.js?raw'
import wasmAsset from 'libass-wasm/dist/js/subtitles-octopus-worker.wasm?url'

import fallbackFontAsset from './assets/fonts/SourceHanSansSC-Regular.otf?url'

const mib = 1024 * 1024

// XHR also supports the APK's file:// assets; fetch does not on every WebView.
export function loadSubtitleAsset(url: string, limit: number, signal: AbortSignal, timeout = 30_000): Promise<ArrayBuffer> {
  return new Promise((resolve, reject) => {
    const request = new XMLHttpRequest()
    const finish = (error?: Error) => {
      signal.removeEventListener('abort', abort)
      if (error) reject(error)
      else resolve(request.response as ArrayBuffer)
    }
    const abort = () => { request.abort(); finish(new DOMException('Cancelled', 'AbortError')) }
    if (signal.aborted) { abort(); return }
    request.open('GET', url)
    request.responseType = 'arraybuffer'
    request.timeout = timeout
    request.onprogress = (event) => {
      if (event.loaded > limit || (event.lengthComputable && event.total > limit)) {
        finish(new Error('Subtitle resource is too large'))
        request.abort()
      }
    }
    request.onload = () => {
      const data = request.response as ArrayBuffer | null
      if (!((request.status >= 200 && request.status < 300) || request.status === 0)
        || !data?.byteLength || data.byteLength > limit) finish(new Error('Subtitle resource unavailable'))
      else finish()
    }
    request.onerror = request.ontimeout = () => finish(new Error('Subtitle resource unavailable'))
    signal.addEventListener('abort', abort, { once: true })
    request.send()
  })
}

export type AssRenderer = {
  render(time: number): void
  resize(width: number, height: number): void
  dispose(): void
}

// Keep the unmodified, version-pinned libass WASM worker behind a small canvas
// adapter. Only raw subtitle/font bytes reach it, never authenticated URLs.
export async function createAssRenderer(canvas: HTMLCanvasElement, source: string, fontUrls: string[],
  signal: AbortSignal, onError: () => void): Promise<AssRenderer> {
  if (signal.aborted) throw new DOMException('Cancelled', 'AbortError')
  const blobs: string[] = []
  let worker: Worker | undefined
  let disposed = false
  let paint = 0
  let startupTimer = 0
  const context = canvas.getContext('2d')
  if (!context) throw new Error('Subtitle canvas unavailable')
  const blobUrl = (data: BlobPart, type: string) => {
    const url = URL.createObjectURL(new Blob([data], { type }))
    blobs.push(url)
    return url
  }
  const dispose = () => {
    if (disposed) return
    disposed = true
    signal.removeEventListener('abort', dispose)
    window.clearTimeout(startupTimer)
    cancelAnimationFrame(paint)
    worker?.terminate()
    context.clearRect(0, 0, canvas.width, canvas.height)
    blobs.forEach((url) => URL.revokeObjectURL(url))
  }
  const fail = () => { if (!disposed) { dispose(); onError() } }
  signal.addEventListener('abort', dispose, { once: true })
  try {
    const [wasm, fallback] = await Promise.all([
      loadSubtitleAsset(new URL(wasmAsset, document.baseURI).href, 8 * mib, signal),
      loadSubtitleAsset(new URL(fallbackFontAsset, document.baseURI).href, 24 * mib, signal),
    ])
    if (disposed || signal.aborted) throw new DOMException('Cancelled', 'AbortError')
    const fonts: string[] = []
    let fontBytes = 0
    const fontDeadline = performance.now() + 15_000
    // Bound attachment memory and concurrency; a missing optional font falls
    // back to bundled Source Han Sans instead of disabling the entire track.
    for (const url of fontUrls.slice(0, 24)) {
      const remaining = Math.floor(fontDeadline - performance.now())
      if (remaining <= 0) break
      try {
        const data = await loadSubtitleAsset(url, Math.min(20 * mib, 48 * mib - fontBytes), signal, Math.min(10_000, remaining))
        if (disposed || signal.aborted) throw new DOMException('Cancelled', 'AbortError')
        fontBytes += data.byteLength
        fonts.push(blobUrl(data, 'application/octet-stream'))
        if (fontBytes >= 48 * mib) break
      } catch {
        if (disposed || signal.aborted) throw new DOMException('Cancelled', 'AbortError')
      }
    }
    const wasmUrl = blobUrl(wasm, 'application/wasm')
    const fallbackUrl = blobUrl(fallback, 'application/octet-stream')
    const bootstrap = `var Module = { locateFile: function () { return ${JSON.stringify(wasmUrl)}; } };\n`
      + 'var console = {log:function(){},debug:function(){},info:function(){},warn:function(){},error:function(){}};\n'
    worker = new Worker(blobUrl(bootstrap + workerSource, 'text/javascript'))
    worker.onerror = fail
    worker.onmessageerror = fail
    type Frame = { x: number; y: number; w: number; h: number; buffer: ArrayBuffer }
    let pending: Frame[] = []
    worker.onmessage = ({ data }) => {
      if (disposed) return
      // Never execute worker-supplied window methods or export its log text.
      if (data.target === 'ready') window.clearTimeout(startupTimer)
      else if (data.target === 'tick') worker?.postMessage({ target: 'tock', id: data.id })
      else if (data.target === 'setimmediate') worker?.postMessage({ target: 'setimmediate' })
      else if (data.target === 'canvas' && data.op === 'renderCanvas') {
        window.clearTimeout(startupTimer)
        pending = data.canvases
        if (!paint) paint = requestAnimationFrame(() => {
          paint = 0
          if (disposed) return
          context.clearRect(0, 0, canvas.width, canvas.height)
          // wasm-blend returns a single precomposited RGBA image, including
          // overlapping layers, alpha, vector drawings and animated overrides.
          for (const frame of pending) {
            if (frame.w > 0 && frame.h > 0) context.putImageData(
              new ImageData(new Uint8ClampedArray(frame.buffer), frame.w, frame.h), frame.x, frame.y)
          }
        })
      }
    }
    startupTimer = window.setTimeout(fail, 30_000)
    worker.postMessage({
      target: 'worker-init', preMain: true, width: canvas.width, height: canvas.height,
      subContent: source, fonts, availableFonts: {}, fallbackFont: fallbackUrl,
      lazyFileLoading: false, renderMode: 'wasm-blend', debug: false,
      targetFps: 60, libassMemoryLimit: 32, libassGlyphLimit: 8, dropAllAnimations: false,
    })
    return {
      render(time) {
        if (!disposed && Number.isFinite(time)) worker?.postMessage({ target: 'video', currentTime: Math.max(0, time), isPaused: true })
      },
      resize(width, height) {
        if (disposed || width <= 0 || height <= 0) return
        canvas.width = width
        canvas.height = height
        worker?.postMessage({ target: 'canvas', width, height })
      },
      dispose,
    }
  } catch (error) { dispose(); throw error }
}
