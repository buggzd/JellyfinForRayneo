import { Info } from 'lucide-react'
import type Hls from 'hls.js'
import { memo, useEffect, useState, type RefObject } from 'react'
import type { PlaybackPlan } from './jellyfin'
import { getNativeHardwareVideoCodecs } from './runtime'
import { usePresence } from './feedback'
import { playbackInfoRows, playbackMethodLabel, samplePlaybackStats, type PlaybackStats, type PlaybackInfoSource } from './playbackInfo'
import './playbackInfo.css'

const VideoInfoOverlay = memo(function VideoInfoOverlay({ visible, plan, failed, videoRef, hlsRef, sourceRef }: {
  visible: boolean
  plan: PlaybackPlan | null
  failed: boolean
  videoRef: RefObject<HTMLVideoElement | null>
  hlsRef: RefObject<Hls | null>
  sourceRef: RefObject<PlaybackInfoSource>
}) {
  const mounted = usePresence(visible)
  const [sample, setSample] = useState<{ plan: PlaybackPlan; stats: PlaybackStats } | null>(null)
  useEffect(() => {
    if (!visible || !plan || failed) return
    const refresh = () => {
      if (document.hidden || !videoRef.current) return
      if (sourceRef.current.plan !== plan) {
        setSample({ plan, stats: {} })
        return
      }
      const stats = samplePlaybackStats(videoRef.current, hlsRef.current)
      setSample({ plan, stats: videoRef.current.readyState >= 1 ? { ...stats, ...sourceRef.current.codecs } : {} })
    }
    refresh()
    const timer = window.setInterval(refresh, 1000)
    document.addEventListener('visibilitychange', refresh)
    return () => {
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', refresh)
    }
  }, [visible, plan, failed, videoRef, hlsRef, sourceRef])

  if (!mounted) return null
  const rows = plan ? playbackInfoRows(plan, sample?.plan === plan ? sample.stats : {}, getNativeHardwareVideoCodecs()) : null
  return (
    <aside className={`playback-info${visible ? '' : ' is-leaving'}`} aria-label="视频信息" aria-hidden={!visible} aria-live="off">
      <header className="playback-info__header">
        <h2><Info size={17} aria-hidden="true" />视频信息</h2>
        <span>{failed ? '播放中断' : playbackMethodLabel(plan)}</span>
      </header>
      {rows ? <>
        <section aria-label="当前播放">
          <h3>当前播放</h3>
          <dl>{rows.current.map(({ label, value }) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
        </section>
        <p className="playback-info__note">WebView 未公开本次实际硬解 / 软解状态</p>
        <section aria-label="原始媒体">
          <h3>原始媒体</h3>
          <dl>{rows.original.map(({ label, value }) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
        </section>
      </> : <p className="playback-info__pending">{failed ? '暂时无法读取媒体信息' : '媒体就绪后显示编码与播放参数'}</p>}
    </aside>
  )
})

export default VideoInfoOverlay
