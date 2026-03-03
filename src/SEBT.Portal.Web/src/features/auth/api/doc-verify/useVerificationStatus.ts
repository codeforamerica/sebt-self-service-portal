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
const MAX_INTERVAL_MS = 10000

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
      // Exponential backoff using TanStack Query's built-in fetch counter
      const count = query.state.dataUpdateCount
      const interval = Math.min(1000 * 2 ** Math.max(0, count - 1), MAX_INTERVAL_MS)
      return interval
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
