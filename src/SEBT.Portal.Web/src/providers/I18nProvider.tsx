'use client'

// Initialize i18next before rendering. This side-effect import must run
// inside a 'use client' boundary because it transitively imports
// react-i18next (which calls createContext() at module scope).
import '@/lib/i18n-init'

export { I18nProvider } from '@sebt/design-system/client'
