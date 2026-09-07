import React, { useEffect, useRef, useState } from 'react'
import { Check, Crop, LoaderCircle, Move, RotateCcw, X } from 'lucide-react'
import { BACKGROUND_RATIOS, BACKGROUND_TEXT_COLORS, DEFAULT_BACKGROUND_LAYOUT, backgroundCrop, dragBackgroundCrop } from './backgroundLayout.mjs'
import { useWallpaperContrast } from './useWallpaperContrast'

export function BackgroundArtwork({ background, layout = background.layout, screenAspect, raw = false }) {
  const image = background.dimensions
  const crop = image && backgroundCrop(image.width, image.height, layout, screenAspect)
  if (!crop) return null
  return (
    <svg className="background-artwork" viewBox={`${crop.x} ${crop.y} ${crop.width} ${crop.height}`}
      preserveAspectRatio="xMidYMid slice" aria-hidden="true" style={{ opacity: raw ? 1 : 1 - layout.transparency / 100 }}>
      <image href={background.url} width={image.width} height={image.height} />
    </svg>
  )
}

export default function BackgroundEditor({ background, screenAspect, glassTransparency }) {
  const dialogRef = useRef(null)
  const dragRef = useRef(null)
  const previewRef = useRef(null)
  const [draft, setDraft] = useState(() => ({ ...background.layout }))
  const [view, setView] = useState('crop')
  const [dragging, setDragging] = useState(false)
  const [error, setError] = useState('')
  const image = background.dimensions
  const crop = image && backgroundCrop(image.width, image.height, draft, screenAspect)
  useWallpaperContrast(previewRef, background, draft, screenAspect, view === 'preview', undefined, glassTransparency)

  useEffect(() => {
    const dialog = dialogRef.current
    const opener = background.editorOpener.current
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    dialog.showModal()
    return () => {
      dialog.close()
      document.body.style.overflow = overflow
      if (opener?.isConnected) opener.focus({ preventScroll: true })
    }
  }, [])

  const update = (patch, nextView = 'crop') => {
    setDraft((value) => ({ ...value, ...patch }))
    setView(nextView)
    setError('')
  }
  const move = (event) => {
    const drag = dragRef.current
    if (!drag || drag.id !== event.pointerId) return
    setDraft(dragBackgroundCrop(drag.layout, drag.crop, image, drag.frame, event.clientX - drag.x, event.clientY - drag.y))
  }
  const endDrag = (event) => {
    if (dragRef.current?.id !== event.pointerId) return
    dragRef.current = null
    setDragging(false)
  }
  const apply = async () => {
    setError('')
    if (await background.applyLayout(draft)) background.closeEditor()
    else setError('调整未能保存，请重试。原来的背景设置仍然保留。')
  }
  const aspect = view === 'preview' ? screenAspect : crop ? crop.width / crop.height : screenAspect

  return (
    <dialog ref={dialogRef} className="background-editor" aria-labelledby="background-editor-title"
      onCancel={(event) => { event.preventDefault(); background.cancelEditor() }}>
      <header className="background-editor__header">
        <div><span>LIQUID UI</span><h2 id="background-editor-title">调整背景</h2></div>
        <button type="button" disabled={background.saving} onClick={background.cancelEditor} aria-label="取消背景调整" autoFocus><X size={20} /></button>
      </header>
      <div className="background-editor__body">
        <div className="background-editor__views" role="group" aria-label="背景预览方式">
          <button type="button" aria-pressed={view === 'crop'} onClick={() => setView('crop')}><Crop size={15} />裁切图片</button>
          <button type="button" aria-pressed={view === 'preview'} onClick={() => setView('preview')}>界面预览</button>
        </div>
        <div className="background-editor__canvas">
          <div ref={previewRef} data-wallpaper-scope="preview" className={`background-crop-frame ${view === 'preview' ? 'is-preview' : ''} ${dragging ? 'is-dragging' : ''}`}
            style={{ width: `min(310px, calc(var(--background-preview-height) * ${aspect}))`, aspectRatio: aspect }}>
            <BackgroundArtwork background={background} layout={draft} screenAspect={screenAspect} raw={view === 'crop'} />
            {view === 'crop' ? (
              <div className="background-crop-handle" role="group" aria-label="拖动图片调整裁切位置" tabIndex={0}
                aria-describedby="background-crop-help"
                onPointerDown={(event) => {
                  if (background.saving || !crop || !event.isPrimary || (event.pointerType === 'mouse' && event.button !== 0)) return
                  event.currentTarget.setPointerCapture(event.pointerId)
                  dragRef.current = { id: event.pointerId, x: event.clientX, y: event.clientY, crop, layout: { ...draft }, frame: event.currentTarget.getBoundingClientRect() }
                  setDragging(true)
                }}
                onPointerMove={move}
                onPointerUp={(event) => { move(event); endDrag(event) }}
                onPointerCancel={endDrag}
                onLostPointerCapture={endDrag}
                onKeyDown={(event) => {
                  if (background.saving) return
                  const delta = event.shiftKey ? 50 : 10
                  const key = { ArrowLeft: ['x', -delta], ArrowRight: ['x', delta], ArrowUp: ['y', -delta], ArrowDown: ['y', delta] }[event.key]
                  if (key) { event.preventDefault(); update({ [key[0]]: Math.max(0, Math.min(1000, draft[key[0]] + key[1])) }) }
                  if (event.key === 'Home') { event.preventDefault(); update({ x: 500, y: 500 }) }
                }}>
                <i /><i /><i /><i />
                <span><Move size={16} />拖动定位</span>
              </div>
            ) : (
              <div className="background-interface-preview" aria-hidden="true">
                <span data-wallpaper-text="">09:41</span><strong data-wallpaper-text="">我的设备</strong>
                <div className="background-interface-preview__device" data-liquid-surface=""><img src={`${import.meta.env.BASE_URL}art/rayneo-air-3s.webp`} alt="" /><b data-wallpaper-text="glass">RayNeo Air 3S</b></div>
                <div className="background-interface-preview__remote">进入触控板</div>
                <div className="background-interface-preview__server" data-liquid-surface=""><span data-wallpaper-text="glass">Jellyfin 媒体库</span></div>
              </div>
            )}
          </div>
        </div>
        <p className="background-editor__hint" id="background-crop-help">{view === 'crop'
          ? '拖动图片或使用方向键，也可用下方滑杆精确定位。'
          : '背景会铺满手机屏幕，透明度越高，图片越淡。'}</p>
        <fieldset className="background-editor__controls" disabled={background.saving}>
          <legend>裁切与外观</legend>
          <div className="background-editor__label" id="background-text-label"><strong>文字配色</strong><small>用于背景上的文字</small></div>
          <div className="background-text-colors" role="group" aria-labelledby="background-text-label">
            {BACKGROUND_TEXT_COLORS.map(({ value, label }) => (
              <label key={value}><input type="radio" name="background-text-color" value={value} checked={draft.textColor === value}
                onChange={() => update({ textColor: value }, 'preview')} /><span><i className={`text-color-swatch is-${value}`} aria-hidden="true">Aa</i>{label}</span></label>
            ))}
          </div>
          <p className="background-text-help">{draft.textColor === 'auto'
            ? background.dimensions && !background.samples ? '暂时无法自动识别，可手动选择浅色或深色。' : '根据文字所在区域的明暗自动调整，裁切和透明度也会一起考虑。'
            : `背景文字固定为${draft.textColor === 'light' ? '浅色' : '深色'}，可切回自动配色。`} 玻璃卡片内的文字也会同步调整。</p>
          <div className="background-editor__label" id="background-ratio-label"><strong>裁切比例</strong><small>保留导入图片，可再次调整</small></div>
          <div className="background-ratios" role="group" aria-labelledby="background-ratio-label">
            {BACKGROUND_RATIOS.map(({ value, label }) => (
              <label key={value}><input type="radio" name="background-ratio" value={value} checked={draft.ratio === value}
                onChange={() => update({ ratio: value })} /><span>{label}</span></label>
            ))}
          </div>
          <BackgroundSlider label="缩放" id="background-zoom" value={draft.zoom} min={100} max={300}
            display={`${(draft.zoom / 100).toFixed(2)}×`} onChange={(zoom) => update({ zoom })} />
          <div className="background-position-sliders">
            <BackgroundSlider label="水平位置" id="background-x" value={draft.x} min={0} max={1000}
              display={`${Math.round(draft.x / 10)}%`} onChange={(x) => update({ x })} />
            <BackgroundSlider label="垂直位置" id="background-y" value={draft.y} min={0} max={1000}
              display={`${Math.round(draft.y / 10)}%`} onChange={(y) => update({ y })} />
          </div>
          <div className="background-transparency-control">
            <BackgroundSlider label="背景透明度" id="background-transparency" value={draft.transparency} min={0} max={100}
              display={`${draft.transparency}%`} onChange={(transparency) => update({ transparency }, 'preview')} />
            <div className="background-slider-ends"><span>0% · 原图清晰</span><span>100% · 完全透明</span></div>
          </div>
        </fieldset>
        {error && <p className="background-editor__error" role="alert">{error}</p>}
      </div>
      <footer className="background-editor__footer">
        <button type="button" disabled={background.saving} onClick={() => { setDraft({ ...DEFAULT_BACKGROUND_LAYOUT }); setError('') }}><RotateCcw size={15} />重置调整</button>
        <button type="button" className="background-editor__apply" disabled={background.saving || !crop} onClick={apply}>
          {background.saving ? <LoaderCircle className="is-spinning" size={16} /> : <Check size={16} />}{background.saving ? '正在保存…' : '应用背景'}
        </button>
      </footer>
    </dialog>
  )
}

function BackgroundSlider({ label, id, value, min, max, display, onChange }) {
  return (
    <div className="background-slider">
      <label className="background-editor__label" htmlFor={id}><strong>{label}</strong><output htmlFor={id}>{display}</output></label>
      <input id={id} type="range" min={min} max={max} step="1" value={value} aria-valuetext={display} onChange={(event) => onChange(Number(event.target.value))} />
    </div>
  )
}
