// Comes from runtime config (APPLICATION_URL) rather than the CSV-driven
// `applyOnlineLink` translation because the source-of-truth Google Sheet still
// points at the legacy CO `/SEBT/s/?language=en_US` URL.

// Map i18next locale codes to the language param PEAK expects on its URL.
// Unknown locales fall back to en_US.
const PEAK_LANG_BY_LOCALE: Record<string, string> = {
  en: 'en_US',
  es: 'es_US'
}

interface ApplyHrefConfig {
  applicationUrl?: string | undefined
  /** The URL is Colorado's PEAK form, which takes a `language` param. */
  peakParams: boolean
}

/**
 * Returns the configured apply-form URL, or null when none is configured.
 * Callers must hide their apply UI on null.
 */
export function getApplyHref(
  locale: string,
  { applicationUrl, peakParams }: ApplyHrefConfig
): string | null {
  if (!applicationUrl) {
    return null
  }
  if (!peakParams) {
    return applicationUrl
  }
  const url = new URL(applicationUrl)
  url.searchParams.set('language', PEAK_LANG_BY_LOCALE[locale] ?? 'en_US')
  return url.toString()
}
