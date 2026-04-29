import { z } from 'zod'

/**
 * Zod schema for GET /api/auth/status. Returned only on 200 — a 401 means the
 * caller has no valid session cookie. Carries the non-sensitive session claims
 * the SPA needs for IAL gating, analytics, and UI state, since the JWT itself
 * lives in an HttpOnly cookie and cannot be decoded client-side.
 */
export const AuthorizationStatusResponseSchema = z.object({
  isAuthorized: z.boolean(),
  email: z.string().nullish(),
  ial: z.enum(['0', '1', '1plus', '2']).nullish(),
  idProofingStatus: z.number().int().nullish(),
  idProofingCompletedAt: z.number().int().nullish(),
  idProofingExpiresAt: z.number().int().nullish(),
  isCoLoaded: z.boolean().nullish()
})

export type AuthorizationStatusResponse = z.infer<typeof AuthorizationStatusResponseSchema>
