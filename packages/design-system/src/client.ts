'use client'

// @sebt/design-system/client — exports that depend on react-i18next
//
// These must be imported from '@sebt/design-system/client' (not the main
// barrel) because react-i18next calls createContext() at module scope,
// which crashes in the React Server Components layer.

// Layout chrome (uses useTranslation from react-i18next)
export { Header } from './components/layout/Header'
export { Footer } from './components/layout/Footer'
export { HelpSection } from './components/layout/HelpSection'
export { LanguageSelector } from './components/layout/LanguageSelector/LanguageSelector'

// Providers
export { I18nProvider } from './providers/I18nProvider'

// i18n runtime helpers
export { initI18n } from './lib/i18n'
export { changeLanguage, getCurrentLanguage, languageNames, supportedLanguages } from './lib/i18n'
// i18next instance — shared singleton used by I18nProvider and app code
export { default as i18n } from './lib/i18n'
