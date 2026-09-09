import { useMutation } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import { StartChallengeResponseSchema, type StartChallengeResponse } from './schema'

const CHALLENGE_START_ENDPOINT = '/challenges'

async function startChallenge(challengeId: string): Promise<StartChallengeResponse> {
  return apiFetch<StartChallengeResponse>(`${CHALLENGE_START_ENDPOINT}/${challengeId}/start`, {
    method: 'GET',
    schema: StartChallengeResponseSchema
  })
}

export function useStartChallenge() {
  return useMutation({
    mutationFn: startChallenge,
    retry: (failureCount, error) => {
      // Don't retry client errors (4xx) — these are validation/auth issues
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      // Retry server errors (5xx) up to 2 times
      return failureCount < 2
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000)
  })
}
