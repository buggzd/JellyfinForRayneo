import { useEffect, useRef, useState } from 'react'

const DATABASE = 'jellyfin-companion-appearance'
const MAX_BYTES = 20 * 1024 * 1024
const MAX_EDGE = 1600

// Browser previews own only their local wallpaper. Android owns its private image file.
async function storedBackground(operation, value) {
  const database = await new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE, 1)
    request.onupgradeneeded = () => request.result.createObjectStore('images')
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(new Error('图片存储不可用'))
  })
  try {
    return await new Promise((resolve, reject) => {
      const transaction = database.transaction('images', operation === 'read' ? 'readonly' : 'readwrite')
      const images = transaction.objectStore('images')
      const request = operation === 'read' ? images.get('background')
        : operation === 'clear' ? images.delete('background') : images.put(value, 'background')
      transaction.oncomplete = () => resolve(request.result)
      transaction.onabort = transaction.onerror = () => reject(new Error('图片未能保存，请重试'))
    })
  } finally {
    database.close()
  }
}

async function prepareBackground(file) {
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || !file.size || file.size > MAX_BYTES) {
    throw new Error('请选择 20 MB 以内的 JPG、PNG 或 WebP 图片')
  }
  const source = URL.createObjectURL(file)
  try {
    const image = new Image()
    image.src = source
    await image.decode()
    if (!image.naturalWidth || !image.naturalHeight || image.naturalWidth > 16000 || image.naturalHeight > 16000
      || image.naturalWidth * image.naturalHeight > 64_000_000) throw new Error('图片尺寸过大，请选择较小的图片')
    const scale = Math.min(1, MAX_EDGE / Math.max(image.naturalWidth, image.naturalHeight))
    const canvas = document.createElement('canvas')
    canvas.width = Math.max(1, Math.round(image.naturalWidth * scale))
    canvas.height = Math.max(1, Math.round(image.naturalHeight * scale))
    const context = canvas.getContext('2d', { alpha: false })
    context.fillStyle = '#ffffff'
    context.fillRect(0, 0, canvas.width, canvas.height)
    context.drawImage(image, 0, 0, canvas.width, canvas.height)
    return await new Promise((resolve, reject) => canvas.toBlob(
      (blob) => blob ? resolve(blob) : reject(new Error('无法处理这张图片，请换一张重试')), 'image/jpeg', 0.88,
    ))
  } finally {
    URL.revokeObjectURL(source)
  }
}

export function usePhoneBackground(nativeState, notify) {
  const native = typeof window.JellyfinNative?.chooseCompanionBackground === 'function'
  const input = useRef(null)
  const pending = useRef(false)
  const generation = useRef(0)
  const [blob, setBlob] = useState(null)
  const [url, setUrl] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (native) return
    let active = true
    const started = generation.current
    void storedBackground('read').then((saved) => {
      if (active && started === generation.current && saved instanceof Blob && saved.size <= MAX_BYTES && saved.type === 'image/jpeg') setBlob(saved)
    }).catch(() => { /* Import will explain unavailable storage if the user chooses an image. */ })
    return () => { active = false }
  }, [native])

  useEffect(() => {
    if (!blob) { setUrl(''); return }
    const objectUrl = URL.createObjectURL(blob)
    setUrl(objectUrl)
    return () => URL.revokeObjectURL(objectUrl)
  }, [blob])

  const importing = native ? Boolean(nativeState?.companionBackgroundBusy) : busy
  const changeFile = async (event) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file || pending.current) return
    generation.current += 1
    pending.current = true
    setBusy(true)
    try {
      const prepared = await prepareBackground(file)
      await storedBackground('write', prepared)
      setBlob(prepared)
      notify('手机背景已更新', 'success')
    } catch (error) {
      notify(error instanceof Error && /^(请选择|图片|无法处理)/.test(error.message)
        ? error.message : '无法读取这张图片，请重新选择', 'error')
    } finally {
      pending.current = false
      setBusy(false)
    }
  }

  const clear = async () => {
    if (importing || pending.current) return false
    if (native) {
      window.JellyfinNative.clearCompanionBackground()
      return true
    }
    generation.current += 1
    pending.current = true
    setBusy(true)
    try {
      await storedBackground('clear')
      setBlob(null)
      return true
    } catch {
      notify('背景未能移除，请重试', 'error')
      return false
    } finally {
      pending.current = false
      setBusy(false)
    }
  }

  const nativeUrl = nativeState?.companionBackground || ''
  return {
    url: native ? (/^https:\/\/appassets\.androidplatform\.net\/CompanionUI\/phone-background\.jpg\?v=[a-f0-9]{32}$/.test(nativeUrl) ? nativeUrl : '') : url,
    busy: importing,
    input,
    changeFile,
    choose: () => {
      if (importing) return
      if (native) window.JellyfinNative.chooseCompanionBackground()
      else input.current?.click()
    },
    clear,
  }
}
