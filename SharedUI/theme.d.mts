export type UiTheme = 'liquid-glass' | 'simpleUI'
export const DEFAULT_UI_THEME: 'liquid-glass'
export const SIMPLE_UI_THEME: 'simpleUI'
export const PREVIEW_THEME_KEY: string
export function normalizeUiTheme(value: unknown): UiTheme
export function applyUiTheme(value: unknown): UiTheme
export function readPreviewTheme(): UiTheme
export function savePreviewTheme(value: unknown): void
