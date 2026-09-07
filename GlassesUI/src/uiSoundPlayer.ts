export const uiSoundNames = ['focus', 'select', 'back', 'home', 'open', 'close', 'toggle-on', 'toggle-off',
  'volume', 'boundary', 'success', 'error', 'notification', 'loading'] as const
export type UiSound = typeof uiSoundNames[number]
export const uiSoundStorageKey = 'rayneo.glasses.ui-sounds.v1'

type Clip = Pick<HTMLAudioElement, 'currentTime' | 'pause' | 'play'>
type PreferenceStorage = Pick<Storage, 'getItem' | 'setItem'>

// The pool belongs only to the glasses document. UI clips never touch the video,
// its volume, audio tracks, native playback state, or Jellyfin reports.
export class UiSoundPlayer {
  private clips = new Map<UiSound, Clip>()
  private active: Clip | null = null
  private lastSound: UiSound | null = null
  private lastTime = -Infinity
  private enabled = true
  private generation = 0
  private activityVersion = 0

  constructor(
    private createClip: (sound: UiSound) => Clip,
    private storage: () => PreferenceStorage,
    private visible: () => boolean,
    private now: () => number = () => performance.now(),
  ) {
    try { this.enabled = storage().getItem(uiSoundStorageKey) !== 'off' } catch { /* Use the default. */ }
  }

  isEnabled = () => this.enabled
  get revision() { return this.activityVersion }

  setEnabled(enabled: boolean) {
    this.enabled = enabled
    if (!enabled) this.stop()
    try {
      this.storage().setItem(uiSoundStorageKey, enabled ? 'on' : 'off')
      return true
    } catch {
      // The choice still applies immediately when storage is unavailable.
      return false
    }
  }

  preload() {
    if (!this.enabled) return
    for (const sound of uiSoundNames) this.clip(sound)
  }

  private clip(sound: UiSound) {
    try {
      let clip = this.clips.get(sound)
      if (!clip) {
        clip = this.createClip(sound)
        this.clips.set(sound, clip)
      }
      return clip
    } catch { return null }
  }

  stop() {
    this.activityVersion++
    this.generation++
    // pause() also aborts a pending play(), so an old clip cannot start after mute.
    try { this.active?.pause() } catch { /* Sound must never block interaction. */ }
    this.active = null
    this.lastSound = null
    this.lastTime = -Infinity
  }

  play(sound: UiSound) {
    this.activityVersion++
    if (!this.enabled || !this.visible()) return
    const time = this.now()
    const interval = sound === 'boundary' ? 280 : sound === 'volume' ? 120 : 70
    if (this.lastSound === sound && time - this.lastTime < interval) return
    const clip = this.clip(sound)
    if (!clip) return
    this.stop()
    this.lastSound = sound
    this.lastTime = time
    this.active = clip
    const generation = this.generation
    try {
      clip.currentTime = 0
      void clip.play().catch(() => {
        // A rejected/aborted play has no delayed retry or replay queue.
        if (this.generation === generation) this.active = null
      })
    } catch {
      this.active = null
    }
  }
}
