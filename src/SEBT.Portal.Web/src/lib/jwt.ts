/**
 * Client-side JWT payload decoding for reading claims without verification.
 * The API validates tokens; we only decode to make UI decisions such as IAL-based redirects.
 */

/** IAL claim values in the portal JWT */
export type IalClaimValue = '0' | '1' | '1plus' | '2'

/**
 * Decodes the payload (middle part) of a JWT without verification.
 * Returns null if the token is invalid or cannot be parsed.
 */
export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split('.')
    if (parts.length !== 3) return null
    const payload = parts[1]
    if (!payload) return null
    // Base64url to standard base64
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const json = atob(base64)
    return JSON.parse(json) as Record<string, unknown>
  } catch {
    return null
  }
}

/**
 * Gets the IAL claim from a portal JWT.
 * Returns the raw claim value ("0", "1", "1plus", "2") or null if missing/invalid.
 */
export function getIalFromToken(token: string): IalClaimValue | null {
  const payload = decodeJwtPayload(token)
  if (!payload) return null
  const ial = payload.ial ?? payload.ial_level
  if (typeof ial !== 'string') return null
  if (ial === '0' || ial === '1' || ial === '1plus' || ial === '2') return ial as IalClaimValue
  return null
}

/**
 * True if the user has at least IAL1+ (can view address PII, etc.).
 */
export function hasIal1Plus(token: string | null): boolean {
  if (!token) return false
  const ial = getIalFromToken(token)
  return ial === '1plus' || ial === '2'
}

/** Portal JWT claim: ID proofing completion time as Unix seconds (see JwtTokenService). */
export const ID_PROOFING_COMPLETED_AT_CLAIM = 'id_proofing_completed_at'

/** Portal JWT claim: when IdP-bounded proofing expires (Unix seconds; omitted if unknown). */
export const ID_PROOFING_EXPIRES_AT_CLAIM = 'id_proofing_expires_at'

const MS_PER_YEAR = 365.25 * 24 * 60 * 60 * 1000

function parseUnixSecondsClaim(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value !== '') {
    const n = Number(value)
    if (Number.isFinite(n)) return n
  }
  return null
}

/**
 * Parses the public max-age env for ID proofing staleness (default 5 years). Caps absurd values.
 * Wired from build env by IalGuard for OIDC step-up flows.
 */
export function parseIdProofingMaxAgeYears(raw: string | undefined): number {
  if (raw === undefined || raw === '') return 5
  const n = Number(raw)
  if (!Number.isFinite(n) || n <= 0) return 5
  return Math.min(n, 100)
}

/**
 * True if ID proofing is still valid for IAL gating: {@link ID_PROOFING_COMPLETED_AT_CLAIM} is within
 * the last `maxAgeYears` years, and when {@link ID_PROOFING_EXPIRES_AT_CLAIM} is present, `now` is
 * strictly before that instant (IdP time-bounded credential).
 */
export function isIdProofingCompletionFresh(token: string | null, maxAgeYears: number): boolean {
  if (!token) return false
  const payload = decodeJwtPayload(token)
  if (!payload) return false
  const unixSec = parseUnixSecondsClaim(payload[ID_PROOFING_COMPLETED_AT_CLAIM])
  if (unixSec === null) return false
  const completedAtMs = unixSec * 1000
  const maxMs = maxAgeYears * MS_PER_YEAR
  if (Date.now() - completedAtMs > maxMs) return false

  const expiresSec = parseUnixSecondsClaim(payload[ID_PROOFING_EXPIRES_AT_CLAIM])
  if (expiresSec !== null) {
    const expiresAtMs = expiresSec * 1000
    if (Date.now() >= expiresAtMs) return false
  }

  return true
}
