import { UiSoundPlayer, uiSoundNames, type UiSound } from './uiSoundPlayer'
export type { UiSound } from './uiSoundPlayer'

export const uiSounds = new UiSoundPlayer(
  (sound) => {
    // HTMLAudio loads relative file:///android_asset URLs as well as browser URLs.
    // Keep the supplied PCM files and their relative loudness unchanged.
    const clip = new Audio(new URL(`./assets/sounds/mori_${sound}.wav`, document.baseURI).href)
    clip.preload = 'auto'
    return clip
  },
  () => window.localStorage,
  () => !document.hidden,
)

export function installUiSounds() {
  uiSounds.preload()
  const clicks = new WeakMap<MouseEvent, { sound: UiSound; revision: number }>()
  const onClickStart = (event: MouseEvent) => {
    const button = event.target instanceof Element ? event.target.closest('button') : null
    if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true'
      || button.closest('[inert], .remote-tutorial')) return
    const sound = button.dataset.uiSound ?? 'select'
    if (!uiSoundNames.includes(sound as UiSound)) return
    clicks.set(event, { sound: sound as UiSound, revision: uiSounds.revision })
  }
  const onClick = (event: MouseEvent) => {
    const click = clicks.get(event)
    // React's root handler runs before document bubbling. Its more specific
    // result (e.g. seek boundary or notification) wins over the generic click.
    // A microtask in capture can run too early for a browser-generated event.
    if (click && !event.defaultPrevented && uiSounds.revision === click.revision) uiSounds.play(click.sound)
  }
  const onVolume = (event: Event) => {
    const command = (event as CustomEvent<unknown>).detail
    if (typeof command !== 'string' || !/^volume:\d{1,3}$/.test(command)) return
    const value = Number(command.slice(7))
    if (value > 0 && value <= 100) uiSounds.play('volume')
  }
  const onHidden = () => { if (document.hidden) uiSounds.stop() }
  const onPageHide = () => uiSounds.stop()
  document.addEventListener('click', onClickStart, true)
  document.addEventListener('click', onClick)
  document.addEventListener('visibilitychange', onHidden)
  window.addEventListener('pagehide', onPageHide)
  // Direction/enter/back use the keyboard path only; Android also emits this
  // notification for the same gesture. Volume has no paired keyboard event.
  window.addEventListener('rayneo-remote-command', onVolume)
  return () => {
    uiSounds.stop()
    document.removeEventListener('click', onClickStart, true)
    document.removeEventListener('click', onClick)
    document.removeEventListener('visibilitychange', onHidden)
    window.removeEventListener('pagehide', onPageHide)
    window.removeEventListener('rayneo-remote-command', onVolume)
  }
}
