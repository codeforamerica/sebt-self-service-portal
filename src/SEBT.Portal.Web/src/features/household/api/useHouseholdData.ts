'use client'

import { useQuery } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'

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
 * When the API returns 403 with a `requiredIal` extension, the user's IAL is
 * below the minimum required by their cases. By default the hook redirects
 * to `/login/id-proofing` and exposes `requiresProofing` so consumers can
 * render a loading state during the redirect.
 *
 * @param options.redirectOnInsufficientIal - Whether to auto-redirect on 403.
 *   Defaults to `true`. Set to `false` to handle the 403 yourself (e.g., show
 *   an inline prompt instead of redirecting).
 * @returns TanStack Query result with household data, plus `requiresProofing` flag
 */
export function useHouseholdData({ redirectOnInsufficientIal = true } = {}) {
  const router = useRouter()

  const query = useQuery({
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

  // A 403 with requiredIal in the response body means the user's IAL is below
  // the minimum required by their cases. Redirect to ID proofing.
  const requiresProofing =
    query.error instanceof ApiError &&
    query.error.status === 403 &&
    'requiredIal' in ((query.error.data as Record<string, unknown>) ?? {})

  useEffect(() => {
    if (requiresProofing && redirectOnInsufficientIal) {
      router.push('/login/id-proofing')
    }
  }, [requiresProofing, redirectOnInsufficientIal, router])

  return { ...query, requiresProofing }
}
