/**
 * Classifies OAuth/OIDC error redirects on `/callback` so we never surface Ping/Socure
 * connector payloads or other structured IdP internals to users.
 */

export type IdpCallbackClassification =
  | { type: 'stepUpDeclined' }
  | { type: 'idpRedirect'; safeDetail?: string }

/** Signals that `error_description` is an encoded blob, not user-facing prose. */
const structuredPayloadPatterns: RegExp[] = [
  /interactionId/i,
  /connectorId/i,
  /capabilityName/i,
  /socureSdkKey/i,
  /errorResponse/i,
  /"errors"\s*:/,
  /\{"id":/
]

const consentDeclinedPatterns: RegExp[] = [
  /user\s+denied\s+consent/i,
  /user\s+opted\s+out/i,
  /denied\s+consent/i
]

const maxSafeHumanDetailLength = 200

export function tryDecodeOAuthErrorDescription(raw: string): string {
  const trimmed = raw.trim()
  if (!trimmed) return ''
  try {
    return decodeURIComponent(trimmed.replace(/\+/g, ' '))
  } catch {
    return trimmed.replace(/\+/g, ' ')
  }
}

export function looksLikeStructuredIdpErrorPayload(description: string): boolean {
  const s = description.trim()
  if (!s) return false
  if (s.length > 400) return true
  const first = s.trimStart()[0]
  if (first === '{' || first === '[') return true
  return structuredPayloadPatterns.some((re) => re.test(s))
}

function looksLikeConsentDeclined(description: string, oauthErrorCode: string | null): boolean {
  const code = oauthErrorCode?.trim().toLowerCase()
  if (code === 'access_denied') return true
  return consentDeclinedPatterns.some((re) => re.test(description))
}

/** Allow short IdP phrases only — blocks JSON-ish / connector garbage. */
export function sanitizeHumanOAuthErrorDetail(description: string): string | undefined {
  const decoded = tryDecodeOAuthErrorDescription(description)
  if (!decoded || looksLikeStructuredIdpErrorPayload(decoded)) return undefined
  if (decoded.length > maxSafeHumanDetailLength) return undefined
  if (/[\[\]{}\\]/.test(decoded)) return undefined
  if (!/^[\w\s.,;:'"!?()/%+\-]*$/u.test(decoded)) return undefined
  return decoded.replace(/\s+/g, ' ').trim()
}

/**
 * Ping/MyCO may put megabytes of nested JSON in `error_description` when Socure consent fails.
 * Treat consent-related signals as a dedicated UX even inside structured payloads.
 */
export function classifyIdpOAuthRedirectError(
  oauthErrorCode: string | null,
  errorDescription: string | null
): IdpCallbackClassification {
  const decoded = tryDecodeOAuthErrorDescription(errorDescription ?? '')
  if (looksLikeConsentDeclined(decoded, oauthErrorCode)) {
    return { type: 'stepUpDeclined' }
  }
  if (looksLikeStructuredIdpErrorPayload(decoded)) {
    return { type: 'idpRedirect' }
  }
  const safeDetail = sanitizeHumanOAuthErrorDetail(decoded)
  return safeDetail !== undefined ? { type: 'idpRedirect', safeDetail } : { type: 'idpRedirect' }
}
