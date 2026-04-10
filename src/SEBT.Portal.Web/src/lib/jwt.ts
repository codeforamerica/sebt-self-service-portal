/**
 * Helpers for reading non-sensitive session claims exposed via GET /auth/status.
 * The underlying JWT lives in an HttpOnly session cookie and cannot be decoded client-side.
 */

import type { SessionInfo } from '@/features/auth/context'

const MS_PER_YEAR = 365.25 * 24 * 60 * 60 * 1000

/** True if the user has at least IAL1+ (can view address PII, etc.). */
export function hasIal1Plus(session: SessionInfo | null): boolean {
  return session?.ial === '1plus' || session?.ial === '2'
}

/**
 * Parses the public max-age env for ID proofing staleness (default 5 years).
 * Wired from build env by IalGuard for OIDC step-up flows.
 */
export function parseIdProofingMaxAgeYears(raw: string | undefined): number {
  if (raw === undefined || raw === '') return 5
  const n = Number(raw)
  if (!Number.isFinite(n) || n <= 0) return 5
  return Math.min(n, 100)
}

/**
 * True if ID proofing is still valid for IAL gating: idProofingCompletedAt is within
 * the last `maxAgeYears` years, and when idProofingExpiresAt is present, now is strictly
 * before that instant (IdP time-bounded credential).
 */
export function isIdProofingCompletionFresh(
  session: SessionInfo | null,
  maxAgeYears: number
): boolean {
  if (!session?.idProofingCompletedAt) return false
  const completedAtMs = session.idProofingCompletedAt * 1000
  const maxMs = maxAgeYears * MS_PER_YEAR
  if (Date.now() - completedAtMs > maxMs) return false

  if (session.idProofingExpiresAt != null) {
    const expiresAtMs = session.idProofingExpiresAt * 1000
    if (Date.now() >= expiresAtMs) return false
  }

  return true
}
