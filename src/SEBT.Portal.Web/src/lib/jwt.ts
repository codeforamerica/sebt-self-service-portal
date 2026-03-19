/**
 * Client-side JWT payload decoding for reading claims without verification.
 * The API validates tokens; we only decode to make UI decisions (e.g. IAL-based redirects).
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
