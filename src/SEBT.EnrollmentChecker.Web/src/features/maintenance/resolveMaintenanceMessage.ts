/**
 * Decides what text (if any) the maintenance banner renders.
 *
 * Banner copy is runtime configuration (AWS AppConfig via the features endpoint), not
 * locale-bundle content, so ops can update it without a deployment. The backend sends a
 * per-language map (e.g. { en: '...', es: '...' }); this function owns the selection and
 * fallback policy.
 *
 * Policy: active language first (regional variants like 'es-US' match their base
 * language, keys match case-insensitively), then English, otherwise hide the banner.
 * A user's selected language never silently shows another language's copy except the
 * English fallback. Empty or whitespace-only values are treated as missing so a blank
 * config entry can't render an empty banner.
 *
 * @param enabled banner toggle from the backend
 * @param messages per-language banner copy from the backend, keyed by ISO language code
 * @param language the active i18next language (e.g. 'en', 'es', 'es-US')
 * @returns the text to render, or null to hide the banner
 */
export function resolveMaintenanceMessage(
  enabled: boolean,
  messages: Record<string, string>,
  language: string | undefined
): string | null {
  if (!enabled) {
    return null
  }

  const normalized = new Map<string, string>()
  for (const [key, value] of Object.entries(messages)) {
    if (value.trim() !== '') {
      normalized.set(key.toLowerCase(), value)
    }
  }

  const baseLanguage = (language ?? 'en').toLowerCase().split('-')[0]
  const activeMessage = normalized.get(baseLanguage)
  if (activeMessage !== undefined) {
    return activeMessage
  }

  const englishMessage = normalized.get('en')
  if (englishMessage !== undefined) {
    console.warn(
      `No '${baseLanguage}' maintenance banner message in configuration; falling back to 'en'.`
    )
    return englishMessage
  }

  console.warn(
    `Maintenance banner is enabled but configuration has no usable message for '${baseLanguage}' or 'en'; hiding banner.`
  )
  return null
}
