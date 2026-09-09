export type Language = 'system' | 'zh-CN' | 'en'
export type Locale = 'zh-CN' | 'en'
export const LANGUAGE_OPTIONS: readonly Language[]
export const PREVIEW_LANGUAGE_KEY: string
export function isLanguage(value: unknown): value is Language
export function normalizeLanguage(value: unknown): Language
export function resolveLanguage(value: unknown, systemLanguage?: string): Locale
export function readPreviewLanguage(): Language
export function savePreviewLanguage(value: unknown): void
export function getLanguage(): Language
export function getLocale(): Locale
export function subscribeLanguage(listener: () => void): () => void
export function applyLanguage(value: unknown, systemLanguage?: string): void
export function t(key: string, values?: Record<string | number, unknown>): string
export function nativeMessage(message?: string): string
