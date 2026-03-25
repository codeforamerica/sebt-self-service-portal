import type { SupportedLanguage } from '../../../lib/i18n'

/** Map language code to translation key for t() lookup */
export const languageTranslationKeys: Record<SupportedLanguage, string> = {
  en: 'english',
  es: 'español',
  am: 'amharic'
}
