import { useMutation, useQueryClient } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import { ResubmitChallengeResponseSchema, type ResubmitChallengeResponse } from './schema'

const CHALLENGE_RESUBMIT_ENDPOINT = '/challenges'

async function resubmitChallenge(challengeId: string): Promise<ResubmitChallengeResponse> {
  return apiFetch<ResubmitChallengeResponse>(
    `${CHALLENGE_RESUBMIT_ENDPOINT}/${challengeId}/resubmit`,
    {
      method: 'POST',
      schema: ResubmitChallengeResponseSchema
    }
  )
}

/**
 * Mutation that opens a fresh docv_stepup challenge after a Socure RESUBMIT verdict (DC-301).
 * The prior challenge stays terminal (Resubmit); the response gives a brand-new challenge ID
 * the caller should swap into the URL and start polling.
 */
export function useResubmitChallenge() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: resubmitChallenge,
    onSuccess: async (_data, priorChallengeId) => {
      // Stale verification status from the prior Resubmit challenge would otherwise flash
      // the retry prompt again before the new challenge ID swaps in.
      await queryClient.invalidateQueries({ queryKey: ['verificationStatus', priorChallengeId] })
    },
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 2
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000)
  })
}
