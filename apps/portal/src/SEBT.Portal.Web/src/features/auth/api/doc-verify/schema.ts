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
// 'resubmit' is terminal at Socure but retry-eligible at the portal — the user can
// open a fresh docv_stepup challenge via POST /api/challenges/:id/resubmit.
export const VerificationStatusResponseSchema = z.object({
  status: z.enum(['pending', 'verified', 'rejected', 'resubmit']),
  offboardingReason: z.string().nullish(),
  allowIdRetry: z.boolean().optional()
})

export type VerificationStatusResponse = z.infer<typeof VerificationStatusResponseSchema>

// POST /api/challenges/:id/resubmit — opens a brand-new docv_stepup challenge after a
// Socure RESUBMIT verdict. Returns the new challenge ID + DocV URL for re-capture.
export const ResubmitChallengeResponseSchema = z.object({
  challengeId: z.string().uuid(),
  docvTransactionToken: z.string(),
  docvUrl: z.string().url()
})

export type ResubmitChallengeResponse = z.infer<typeof ResubmitChallengeResponseSchema>
