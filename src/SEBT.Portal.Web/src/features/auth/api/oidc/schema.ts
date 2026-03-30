import { z } from 'zod'

/**
 * Zod schemas for OIDC authentication API responses.
 * Used by COLoginPage (config fetch) and CallbackPage (token exchange).
 */

export const OidcConfigResponseSchema = z.object({
  authorizationEndpoint: z.string().url(),
  tokenEndpoint: z.string().url(),
  clientId: z.string(),
  redirectUri: z.string().url(),
  languageParam: z.string().optional()
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
  .optional()
  .refine((v) => v == null || v === '' || isSafeOidcStepUpReturnPath(v), {
    message: 'returnUrl must be a safe relative path'
  })

export const OidcCompleteLoginResponseSchema = z.object({
  token: z.string(),
  returnUrl: returnUrlAfterOidcSchema
})

export type OidcCompleteLoginResponse = z.infer<typeof OidcCompleteLoginResponseSchema>

/**
 * Server-side schemas for the Next.js OIDC route handler (route.ts).
 * These validate external IdP responses.
 */

export const OidcCallbackRequestSchema = z.object({
  code: z.string(),
  code_verifier: z.string(),
  /** redirect_uri used in the auth request — must match exactly for token exchange. Passed from PKCE storage. */
  redirectUri: z.string().url(),
  // state is validated client-side (PKCE flow) before this request — accepted here for passthrough but not used by the route handler
  state: z.string().optional(),
  stateCode: z.string(),
  isStepUp: z.boolean().optional()
})

export type OidcCallbackRequest = z.infer<typeof OidcCallbackRequestSchema>

export const OidcDiscoveryResponseSchema = z.object({
  token_endpoint: z.string().url(),
  jwks_uri: z.string().url(),
  issuer: z.string().optional(),
  userinfo_endpoint: z.string().url().optional()
})

export type OidcDiscoveryResponse = z.infer<typeof OidcDiscoveryResponseSchema>

export const OidcTokenResponseSchema = z.object({
  id_token: z.string(),
  access_token: z.string().optional(),
  error: z.string().optional(),
  error_description: z.string().optional()
})

export type OidcTokenResponse = z.infer<typeof OidcTokenResponseSchema>
