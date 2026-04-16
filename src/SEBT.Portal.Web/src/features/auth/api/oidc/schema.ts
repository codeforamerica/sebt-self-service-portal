import { z } from 'zod'

/**
 * Zod schemas for OIDC authentication API responses.
 * Used by COLoginPage (config fetch) and CallbackPage (token exchange).
 */

/**
 * the API now generates PKCE server-side and returns `state` + `codeChallenge`
 * + `codeChallengeMethod`. The browser uses these to build the authorization URL but
 * never sees or stores the `code_verifier`. Token exchange also happens server-side,
 * so `tokenEndpoint` is no longer returned.
 */
export const OidcConfigResponseSchema = z.object({
  authorizationEndpoint: z.url(),
  clientId: z.string(),
  redirectUri: z.url(),
  state: z.string(),
  codeChallenge: z.string(),
  codeChallengeMethod: z.literal('S256')
})

export type OidcConfigResponse = z.infer<typeof OidcConfigResponseSchema>

export const OidcCallbackTokenResponseSchema = z.object({
  callbackToken: z.string()
})

export type OidcCallbackTokenResponse = z.infer<typeof OidcCallbackTokenResponseSchema>

/** Max length for post-OIDC relative return path (path + query). */
export const OIDC_RETURN_PATH_MAX_LENGTH = 4096

/** Path segment (before `?`) for scheme checks; query may contain arbitrary values. */
function oidcReturnPathSegment(v: string): string {
  const q = v.indexOf('?')
  return q < 0 ? v : v.slice(0, q)
}

/** Same rules as API `TrySanitizeStepUpReturnUrl`: relative path only, no open redirects. */
export function isSafeOidcStepUpReturnPath(v: string): boolean {
  const t = v.trim()
  if (t.length === 0 || t.length > OIDC_RETURN_PATH_MAX_LENGTH) return false
  if (!t.startsWith('/')) return false
  if (t.startsWith('//')) return false
  if (oidcReturnPathSegment(t).includes('://')) return false
  if (t.includes('\\')) return false
  if (/[\r\n\0]/.test(t)) return false
  return true
}

const returnUrlAfterOidcSchema = z
  .string()
  .nullish()
  .refine((v) => v == null || v === '' || isSafeOidcStepUpReturnPath(v), {
    message: 'returnUrl must be a safe relative path'
  })

export const OidcCompleteLoginResponseSchema = z.object({
  returnUrl: returnUrlAfterOidcSchema
})

export type OidcCompleteLoginResponse = z.infer<typeof OidcCompleteLoginResponseSchema>
