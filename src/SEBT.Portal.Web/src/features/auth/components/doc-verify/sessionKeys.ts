export const SK_CHALLENGE_ID = 'docVerify_challengeId'
export const SK_SUB_STATE = 'docVerify_subState'
export const SK_STILL_CHECKING = 'docv_still_checking'

export type SubState = 'interstitial' | 'capture' | 'pending' | 'resubmit'

/** Remove all DocV session keys. Call before starting a new challenge flow. */
export function clearChallengeContext(): void {
  sessionStorage.removeItem(SK_CHALLENGE_ID)
  sessionStorage.removeItem(SK_SUB_STATE)
  sessionStorage.removeItem(SK_STILL_CHECKING)
}
