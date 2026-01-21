import { useQuery } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import { FeatureFlagsResponseSchema, type FeatureFlagsResponse } from './schema'

async function fetchFeatureFlags(): Promise<FeatureFlagsResponse> {
  return apiFetch<FeatureFlagsResponse>('/features', {
    schema: FeatureFlagsResponseSchema
  })
}

/**
 * Hook to fetch all feature flags from the backend.
 * Feature flags are cached for 5 minutes and retained for 30 minutes.
 *
 * @returns TanStack Query result with feature flags dictionary
 */
export function useFeatureFlags() {
  return useQuery({
    queryKey: ['featureFlags'],
    queryFn: fetchFeatureFlags,
    // Feature flags don't change often - use longer cache times
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 30 * 60 * 1000, // 30 minutes
    refetchOnWindowFocus: false,
    retry: (failureCount, error) => {
      // Don't retry client errors (4xx)
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      // Retry server errors up to 2 times
      return failureCount < 2
    },
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 10000)
  })
}
