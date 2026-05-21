import { getState } from '@sebt/design-system'

// Lives in code (not via the CSV-driven `applyOnlineLink` translation) because
// the source-of-truth Google Sheet still points at the legacy CO
// `/SEBT/s/?language=en_US` URL, and DC's apply form lives at a separate
// state-hosted survey URL with no language variant exposed today.

const PEAK_APPLY_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'
const DC_APPLY_URL = 'https://forms.sunbucks.dc.gov/s3/AppUpdate2026'

// Map i18next locale codes to the language param PEAK expects on its URL.
// Unknown locales fall back to en_US.
const PEAK_LANG_BY_LOCALE: Record<string, string> = {
  en: 'en_US',
  es: 'es_US'
}

export function getApplyHref(locale: string): string {
  const state = getState()
  if (state === 'co') {
    const lang = PEAK_LANG_BY_LOCALE[locale] ?? 'en_US'
    return `${PEAK_APPLY_URL}?language=${lang}`
  }
  if (state === 'dc') {
    return DC_APPLY_URL
  }
  return '/apply'
}
