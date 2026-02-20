import type { SupportedLanguage } from '@/lib/i18n'
import type { StateCode } from '@/lib/state'

export interface LanguageSelectorSubProps {
  languages: Array<{ code: SupportedLanguage; key: string }>
  currentLang: SupportedLanguage
  onLanguageSelect: (lang: SupportedLanguage) => void
  t: (key: string) => string
}

export interface MobileLanguageSelectorProps extends LanguageSelectorSubProps {
  state: StateCode
  languageCodes: readonly SupportedLanguage[]
}
