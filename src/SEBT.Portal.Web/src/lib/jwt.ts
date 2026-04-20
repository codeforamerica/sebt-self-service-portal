/**
 * Helpers for reading non-sensitive session claims exposed via GET /auth/status.
 * The underlying JWT lives in an HttpOnly session cookie and cannot be decoded client-side.
 */

import type { SessionInfo } from '@/features/auth/context'

/** True if the user has at least IAL1+ (can view address PII, etc.). */
export function hasIal1Plus(session: SessionInfo | null): boolean {
  return session?.ial === '1plus' || session?.ial === '2'
}

/**
 * True if ID proofing is still valid: the server-computed `idProofingExpiresAt`
 * claim is present and in the future. The server computes this from
 * `IdProofingCompletedAt + IdProofingValiditySettings.ValidityYears`, so the
 * frontend doesn't need its own max-age configuration.
 */
export function isIdProofingCompletionFresh(session: SessionInfo | null): boolean {
  if (session?.idProofingExpiresAt == null) return false
  const expiresAtMs = session.idProofingExpiresAt * 1000
  return Date.now() < expiresAtMs
}
