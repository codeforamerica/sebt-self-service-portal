import { useQuery } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import { VerificationStatusResponseSchema, type VerificationStatusResponse } from './schema'

const VERIFICATION_STATUS_ENDPOINT = '/id-proofing/status'

async function fetchVerificationStatus(challengeId: string): Promise<VerificationStatusResponse> {
  return apiFetch<VerificationStatusResponse>(
    `${VERIFICATION_STATUS_ENDPOINT}?challengeId=${encodeURIComponent(challengeId)}`,
    {
      method: 'GET',
      schema: VerificationStatusResponseSchema
    }
  )
}

// Exponential backoff: 1s → 2s → 4s → 8s → 10s (capped)
const BASE_INTERVAL_MS = 1000
const MAX_INTERVAL_MS = 10000

// Stop automatic polling after this many fetches (~30 min at max interval).
// Must cover the full challenge expiration window (ChallengeExpirationMinutes, default 30).
const MAX_POLL_COUNT = 180

export function useVerificationStatus(challengeId: string | undefined) {
  return useQuery({
    queryKey: ['verificationStatus', challengeId],
    queryFn: () => fetchVerificationStatus(challengeId!),
    enabled: !!challengeId,
    refetchInterval: (query) => {
      // Stop polling when we have a terminal status
      const status = query.state.data?.status
      if (status === 'verified' || status === 'rejected') {
        return false
      }

      const count = query.state.dataUpdateCount

      // Stop automatic polling after MAX_POLL_COUNT — manual "Check status" still works
      if (count >= MAX_POLL_COUNT) {
        return false
      }

      // Exponential backoff using TanStack Query's built-in fetch counter
      const interval = Math.min(BASE_INTERVAL_MS * 2 ** Math.max(0, count - 1), MAX_INTERVAL_MS)
      return interval
    },
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 2
    },
    retryDelay: (attemptIndex) => Math.min(BASE_INTERVAL_MS * 2 ** attemptIndex, MAX_INTERVAL_MS)
  })
}
