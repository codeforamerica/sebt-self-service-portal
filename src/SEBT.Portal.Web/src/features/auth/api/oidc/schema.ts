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

export const OidcCompleteLoginResponseSchema = z.object({
  token: z.string()
})

export type OidcCompleteLoginResponse = z.infer<typeof OidcCompleteLoginResponseSchema>

/**
 * Server-side schemas for the Next.js OIDC route handler (route.ts).
 * These validate external IdP responses.
 */

export const OidcCallbackRequestSchema = z.object({
  code: z.string(),
  code_verifier: z.string(),
  // state is validated client-side (PKCE flow) before this request — accepted here for passthrough but not used by the route handler
  state: z.string().optional(),
  stateCode: z.string()
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
