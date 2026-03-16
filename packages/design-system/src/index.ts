// @sebt/design-system — public API

// UI primitive types (defined in types.ts, not in the component files themselves)
export type { ButtonProps, ButtonVariant, AlertProps, AlertVariant, InputFieldProps } from './components/ui/types'

// Layout component types
export type { StateProps, HeaderProps, FooterProps, HelpSectionProps, LanguageSelectorProps } from './components/layout/types'

// Provider types
export type { I18nProviderProps } from './providers/types'

// UI primitives
export { Button } from './components/ui/Button'
export { InputField } from './components/ui/InputField'
export { Alert } from './components/ui/Alert'
export { TextLink } from './components/ui/TextLink'
// TextLinkProps is defined in TextLink.tsx itself (not in ui/types.ts)
export type { TextLinkProps } from './components/ui/TextLink'

// Rich text rendering (markdown-to-jsx)
export { RichText } from './components/RichText/RichText'
export type { RichTextProps } from './components/RichText/RichText'

// Layout chrome
export { Header } from './components/layout/Header'
export { Footer } from './components/layout/Footer'
export { HelpSection } from './components/layout/HelpSection'
export { SkipNav } from './components/layout/SkipNav'
export { LanguageSelector } from './components/layout/LanguageSelector/LanguageSelector'

// Providers
export { I18nProvider } from './providers/I18nProvider'

// State configuration
export type { StateCode, StateConfig } from './lib/state'
export { getState, getStateConfig, getStateName, getStateAssetPath } from './lib/state'

// External links
export type { StateLinks, LinkItem } from './lib/links'
export { getStateLinks, getFooterLinks, getHelpLinks } from './lib/links'

// i18n helpers
export { initI18n } from './lib/i18n'
export type { StateResources, SupportedLanguage } from './lib/i18n'
export { changeLanguage, getCurrentLanguage, languageNames, supportedLanguages } from './lib/i18n'
// i18next instance — shared singleton used by I18nProvider and app code
export { default as i18n } from './lib/i18n'
