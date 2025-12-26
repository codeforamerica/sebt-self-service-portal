import type { SupportedLanguage } from '@/lib/i18n'

export interface StateProps {
  state?: string
}

export type HeaderProps = StateProps
export type FooterProps = StateProps
export type HelpSectionProps = StateProps

export interface LanguageSelectorProps extends StateProps {
  languages?: readonly SupportedLanguage[]
}
