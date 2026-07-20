'use client'

import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'

import { clearHouseholdQueryCache } from '@/features/household/api/clearHouseholdQueryCache'

import { useAuth } from '../../context'

/**
 * Drops cached household data when portal identity changes (user switch or logout).
 * Query keys are also scoped by userId; this clears any stale entries eagerly.
 */
export function SessionIdentityCacheSync() {
  const { session } = useAuth()
  const queryClient = useQueryClient()
  const previousUserIdRef = useRef<string | null>(null)

  useEffect(() => {
    const userId = session?.userId ?? null
    const previousUserId = previousUserIdRef.current

    if (previousUserId !== null && previousUserId !== userId) {
      clearHouseholdQueryCache(queryClient)
    }

    previousUserIdRef.current = userId
  }, [session?.userId, queryClient])

  return null
}
