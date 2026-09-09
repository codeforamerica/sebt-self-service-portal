/**
 * TEMPORARY copy for the enrollment-checker rate-limit (HTTP 429) failure.
 *
 * The durable home for this string is the CSV -> locale pipeline, but the
 * content sheet has no rate-limit row yet. Editing the generated locale JSON
 * directly is not viable: `pnpm copy:generate` runs on predev/prebuild and
 * would overwrite any hand-added keys.
 *
 * When a rate-limit key lands in the sheet, delete this module and resolve the
 * message with t() in app/review/page.tsx (the generic submit failure already
 * resolves dev.enrollmentCheckerErrorResponse there).
 *
 * The Spanish string is a draft pending content-team review.
 */

const rateLimitCopy: Record<'en' | 'es', string> = {
  en: "You've made too many requests. Please wait a few minutes and try again.",
  es: 'Has realizado demasiadas solicitudes. Por favor, espera unos minutos e inténtalo de nuevo.'
}

/**
 * Resolves the user-facing message for a rate-limited enrollment-check submission.
 *
 * Call from the render path (not from the submit handler) so the message
 * re-resolves when the active language changes.
 *
 * @param language the active i18next language (e.g. 'en', 'es', 'es-US')
 */
export function getRateLimitErrorMessage(language: string | undefined): string {
  const isSpanish = language?.toLowerCase().startsWith('es') ?? false
  return isSpanish ? rateLimitCopy.es : rateLimitCopy.en
}
