import { getState } from '@sebt/design-system'

// Lives in code (not via the CSV-driven `applyOnlineLink` translation) because
// the source-of-truth Google Sheet still points at the legacy
// `/SEBT/s/?language=en_US` URL.

const PEAK_APPLY_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'

// Map i18next locale codes to the language param PEAK expects on its URL.
// Unknown locales fall back to en_US.
const PEAK_LANG_BY_LOCALE: Record<string, string> = {
  en: 'en_US',
  es: 'es_US'
}

export function getApplyHref(locale: string): string {
  if (getState() === 'co') {
    const lang = PEAK_LANG_BY_LOCALE[locale] ?? 'en_US'
    return `${PEAK_APPLY_URL}?language=${lang}`
  }
  return '/apply'
}
