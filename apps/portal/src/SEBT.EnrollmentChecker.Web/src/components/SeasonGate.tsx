'use client'

import { useEnrollmentSeason } from '@/lib/useEnrollmentSeason'
import type { ReactNode } from 'react'

interface SeasonGateProps {
  children: ReactNode
}

/**
 * Holds page content until the enrollment season is known.
 *
 * Every screen behind this gate reads differently by season, and the landing page
 * swaps outright, so rendering before the first features poll lands would show one
 * season's page and replace it with the other's a moment later. Holding costs a
 * single request on the first page of a visit; React Query serves later navigations
 * from cache, so the gate is open by then.
 *
 * `isResolving` clears on the first failed poll, so a checker that cannot reach the
 * features endpoint renders its open-season copy rather than an empty page.
 */
export function SeasonGate({ children }: SeasonGateProps) {
  const { isResolving } = useEnrollmentSeason()

  if (isResolving) {
    return null
  }

  return <>{children}</>
}
