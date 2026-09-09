import { z } from 'zod'

/**
 * Zod schema for GET /api/auth/status. Carries the non-sensitive session claims
 * the SPA needs for IAL gating, analytics, and UI state, since the JWT itself
 * lives in an HttpOnly cookie and cannot be decoded client-side. Anonymous
 * callers get 200 with `isAuthorized: false` and all other fields null — the
 * probe runs on every page load, so "not signed in" is an answer, not an error.
 */
export const AuthorizationStatusResponseSchema = z.object({
  isAuthorized: z.boolean(),
  /** Stable, non-PII portal user UUID. Surfaced for analytics correlation. */
  userId: z.string().uuid().nullish(),
  email: z.string().nullish(),
  ial: z.enum(['0', '1', '1plus', '2']).nullish(),
  idProofingStatus: z.number().int().nullish(),
  idProofingCompletedAt: z.number().int().nullish(),
  idProofingExpiresAt: z.number().int().nullish(),
  isCoLoaded: z.boolean().nullish(),
  /** Sliding (idle) cookie expiry — Unix epoch seconds. */
  expiresAt: z.number().int().nullish(),
  /** Absolute session lifetime cap — Unix epoch seconds. */
  absoluteExpiresAt: z.number().int().nullish()
})

export type AuthorizationStatusResponse = z.infer<typeof AuthorizationStatusResponseSchema>
