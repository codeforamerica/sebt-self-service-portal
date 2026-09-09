/**
 * Numeric values mirror `SEBT.Portal.Core.Models.Auth.IdProofingStatus` from the API/JWT.
 */
export const IdProofingStatus = {
  NotStarted: 0,
  InProgress: 1,
  Completed: 2,
  Failed: 3,
  Expired: 4
} as const

export type IdProofingStatusNumber = (typeof IdProofingStatus)[keyof typeof IdProofingStatus]

export function isIdProofingStatusCompleted(status: number | null | undefined): boolean {
  return status === IdProofingStatus.Completed
}

/**
 * After OTP, use this when `/auth/status` returned an authenticated session.
 * The user continues ID proofing unless status is {@link IdProofingStatus.Completed}.
 * Any other value (including missing/null claim on that session) routes to proofing.
 * If `login()` returned no session, surface an error instead of calling this.
 */
export function needsIdProofingFlowAfterOtp(status: number | null | undefined): boolean {
  return !isIdProofingStatusCompleted(status)
}

/**
 * Analytics `id_proofed`: true when the workflow completed or a completion timestamp exists
 * (covers legacy/in-flight shapes where status and timestamp may not align).
 */
export function isIdProofedForAnalytics(
  idProofingStatus: number | null | undefined,
  idProofingCompletedAt: number | null | undefined
): boolean {
  return isIdProofingStatusCompleted(idProofingStatus) || Boolean(idProofingCompletedAt)
}
