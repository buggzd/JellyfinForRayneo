import { useSyncExternalStore } from 'react'
import { getLanguage, getLocale, subscribeLanguage } from '../../SharedUI/i18n.mjs'
const snapshot = () => `${getLanguage()}:${getLocale()}`
export function useLanguage() { return useSyncExternalStore(subscribeLanguage, snapshot) }
