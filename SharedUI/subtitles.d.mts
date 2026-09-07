export type SubtitleSize = 'small' | 'normal' | 'large' | 'extra-large'
export const DEFAULT_SUBTITLE_SIZE: 'normal'
export const PREVIEW_SUBTITLE_SIZE_KEY: string
export const SUBTITLE_SIZES: readonly Readonly<{ value: SubtitleSize; label: string; scale: number }>[]
export function isSubtitleSize(value: unknown): value is SubtitleSize
export function normalizeSubtitleSize(value: unknown): SubtitleSize
export function subtitleFontSize(value: unknown): string
export function readPreviewSubtitleSize(): SubtitleSize
export function savePreviewSubtitleSize(value: unknown): void
