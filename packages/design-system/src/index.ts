// @sebt/design-system — public API
//
// This barrel is intentionally split: server-safe exports live here,
// while client-only exports that depend on react-i18next live in ./client.
// Mixing them in a single barrel causes react-i18next's module-level
// createContext() to be evaluated in the RSC layer (where it doesn't exist).

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
export { SummaryBox } from './components/ui/SummaryBox'
export type { SummaryBoxProps } from './components/ui/SummaryBox'

// Rich text rendering (markdown-to-jsx)
export { RichText } from './components/RichText/RichText'
export type { RichTextProps } from './components/RichText/RichText'

// Layout chrome (server-safe — no react-i18next dependency)
export { SkipNav } from './components/layout/SkipNav'

// State configuration
export type { StateCode, StateConfig } from './lib/state'
export { getState, getStateConfig, getStateName, getStateAssetPath } from './lib/state'

// External links
export type { StateLinks, LinkItem } from './lib/links'
export { getStateLinks, getFooterLinks, getHelpLinks } from './lib/links'

// i18n types only (no runtime dependency on react-i18next)
export type { StateResources, SupportedLanguage } from './lib/i18n'
