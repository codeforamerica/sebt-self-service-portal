/**
 * TEMPORARY copy for enrollment-checker submit failures (DC-519).
 *
 * The durable home for these strings is the CSV -> locale pipeline
 * (confirmInfo.submitError / confirmInfo.rateLimitError), but those keys are not in
 * the upstream content sheet yet and a scheduled maintenance window needs user-facing
 * copy now. Editing the generated locale JSON directly is not viable: `pnpm copy:generate`
 * runs on predev/prebuild and would overwrite any hand-added keys.
 *
 * When the real keys land in the sheet, delete this module and restore
 * t('submitError') / t('rateLimitError') in app/review/page.tsx.
 *
 * Note: the maintenance message is date-specific and reads as stale once the window
 * passes. The follow-up copy should be a generic "please try again later" instead.
 * The Spanish strings are drafts pending content-team review.
 */

const maintenanceCopy: Record<'en' | 'es', string> = {
  en: 'The S-EBT Enrollment Checker will be unavailable on Saturday, June 13 from 7:00 a.m. to 3:00 p.m. due to system maintenance.',
  es: 'El verificador de inscripción de S-EBT no estará disponible el sábado 13 de junio de 7:00 a.m. a 3:00 p.m. debido a mantenimiento del sistema.'
}

const rateLimitCopy: Record<'en' | 'es', string> = {
  en: "You've made too many requests. Please wait a few minutes and try again.",
  es: 'Has realizado demasiadas solicitudes. Por favor, espera unos minutos e inténtalo de nuevo.'
}

export type SubmitErrorKind = 'maintenance' | 'rateLimit'

/**
 * Resolves the user-facing message for a failed enrollment-check submission.
 *
 * Call from the render path (not from the submit handler) so the message
 * re-resolves when the active language changes.
 *
 * @param kind     which failure occurred ('rateLimit' for HTTP 429, otherwise 'maintenance')
 * @param language the active i18next language (e.g. 'en', 'es', 'es-US')
 */
export function getSubmitErrorMessage(kind: SubmitErrorKind, language: string | undefined): string {
  const isSpanish = language?.toLowerCase().startsWith('es') ?? false
  if (kind === 'rateLimit') {
    return isSpanish ? rateLimitCopy.es : rateLimitCopy.en
  }
  return isSpanish ? maintenanceCopy.es : maintenanceCopy.en
}
