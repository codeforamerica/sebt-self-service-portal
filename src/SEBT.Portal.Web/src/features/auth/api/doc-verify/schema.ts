import { z } from 'zod'

// GET /challenges/:id/start — JIT token fetch for Socure DocV SDK (D2).
// Called when the user clicks "Continue" on the interstitial, not upfront.
export const StartChallengeResponseSchema = z.object({
  docvTransactionToken: z.string(),
  docvUrl: z.string().url()
})

export type StartChallengeResponse = z.infer<typeof StartChallengeResponseSchema>

// GET /api/id-proofing/status — polling endpoint for async verification (D3).
// Backend receives Socure webhook results and exposes them here.
export const VerificationStatusResponseSchema = z.object({
  status: z.enum(['pending', 'verified', 'rejected']),
  offboardingReason: z.string().optional()
})

export type VerificationStatusResponse = z.infer<typeof VerificationStatusResponseSchema>
