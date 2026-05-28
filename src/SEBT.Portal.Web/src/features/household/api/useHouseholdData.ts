'use client'

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { useEffect, useMemo } from 'react'

import { ApiError, apiFetch } from '@/api'

import { mergeHouseholdCardDetails } from './mergeHouseholdCardDetails'
import { HouseholdDataSchema, type HouseholdData } from './schema'

async function fetchHouseholdData(includeCardDetails = true): Promise<HouseholdData> {
  const query = includeCardDetails ? '' : '?includeCardDetails=false'
  return apiFetch<HouseholdData>(`/household/data${query}`, {
    schema: HouseholdDataSchema
  })
}

const householdRetry = (failureCount: number, error: Error) => {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 2
}

const householdRetryDelay = (attemptIndex: number) => Math.min(1000 * 2 ** attemptIndex, 10000)

export interface UseHouseholdDataOptions {
  /** Whether to auto-redirect on 403 with requiredIal. Defaults to true. */
  redirectOnInsufficientIal?: boolean
  /**
   * When true (CO dashboard with defer_ebt_card_data_loading), loads household
   * without card details first, then fetches card fields for enrolled children.
   */
  deferCardDetailsOnLoad?: boolean
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
 */
export function useHouseholdData({
  redirectOnInsufficientIal = true,
  deferCardDetailsOnLoad = false
}: UseHouseholdDataOptions = {}) {
  const router = useRouter()
  const queryClient = useQueryClient()

  const query = useQuery({
    queryKey: ['householdData'],
    queryFn: () => (deferCardDetailsOnLoad ? fetchHouseholdData(false) : fetchHouseholdData(true)),
    staleTime: 0,
    gcTime: 5 * 60 * 1000, // 5 minutes for back-navigation
    refetchOnWindowFocus: true,
    retry: householdRetry,
    retryDelay: householdRetryDelay
  })

  const shouldFetchCardDetails =
    deferCardDetailsOnLoad && query.isSuccess && (query.data?.summerEbtCases.length ?? 0) > 0

  const cardDetailsQuery = useQuery({
    queryKey: ['householdData', 'cardDetails'],
    queryFn: () => fetchHouseholdData(true),
    enabled: shouldFetchCardDetails,
    staleTime: 0,
    gcTime: 5 * 60 * 1000,
    refetchOnWindowFocus: true,
    retry: householdRetry,
    retryDelay: householdRetryDelay
  })

  const data = useMemo(() => {
    if (!query.data) {
      return query.data
    }

    if (cardDetailsQuery.data) {
      return mergeHouseholdCardDetails(query.data, cardDetailsQuery.data)
    }

    return query.data
  }, [query.data, cardDetailsQuery.data])

  useEffect(() => {
    if (!query.data || !cardDetailsQuery.data) {
      return
    }

    queryClient.setQueryData(
      ['householdData'],
      mergeHouseholdCardDetails(query.data, cardDetailsQuery.data)
    )
  }, [query.data, cardDetailsQuery.data, queryClient])

  const requiresProofing =
    query.error instanceof ApiError &&
    query.error.status === 403 &&
    'requiredIal' in ((query.error.data as Record<string, unknown>) ?? {})

  const isRedirecting = query.error instanceof ApiError && query.error.isRedirecting
  const isError = query.isError && !isRedirecting
  const isLoading = query.isLoading || isRedirecting
  const isLoadingCardDetails =
    shouldFetchCardDetails && (cardDetailsQuery.isLoading || cardDetailsQuery.isFetching)

  useEffect(() => {
    if (requiresProofing && redirectOnInsufficientIal) {
      router.push('/login/id-proofing')
    }
  }, [requiresProofing, redirectOnInsufficientIal, router])

  return { ...query, data, isError, isLoading, requiresProofing, isLoadingCardDetails }
}
