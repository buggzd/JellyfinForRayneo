import { applyLanguage } from '../../SharedUI/i18n.mjs'
// Existing fixtures assert Chinese copy; production defaults still follow the OS.
applyLanguage('zh-CN')
export function resolveI18nImport(code) {
  return code.replaceAll('../../SharedUI/i18n.mjs', new URL('../../SharedUI/i18n.mjs', import.meta.url).href)
}
