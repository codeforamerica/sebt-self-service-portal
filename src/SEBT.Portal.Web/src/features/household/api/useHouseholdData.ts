import { useQuery } from '@tanstack/react-query'

import { ApiError, apiFetch } from '@/api'

import { HouseholdDataSchema, type HouseholdData } from './schema'

async function fetchHouseholdData(): Promise<HouseholdData> {
  return apiFetch<HouseholdData>('/household/data', {
    schema: HouseholdDataSchema
  })
}

/**
 * Hook to fetch household data for the authenticated user.
 * Uses real-time fetching (staleTime: 0) to ensure data freshness
 * per ticket requirement to mitigate stale household/custody data.
 *
 * @returns TanStack Query result with household data
 */
export function useHouseholdData() {
  return useQuery({
    queryKey: ['householdData'],
    queryFn: fetchHouseholdData,
    staleTime: 0,
    gcTime: 5 * 60 * 1000, // 5 minutes for back-navigation
    refetchOnWindowFocus: true,
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
