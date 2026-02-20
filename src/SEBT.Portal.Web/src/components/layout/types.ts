import type { SupportedLanguage } from '@/lib/i18n'
import type { StateCode } from '@/lib/state'

export interface StateProps {
  state?: StateCode
}

export type HeaderProps = StateProps
export type FooterProps = StateProps
export type HelpSectionProps = StateProps

export interface LanguageSelectorProps extends StateProps {
  languages?: readonly SupportedLanguage[]
}
