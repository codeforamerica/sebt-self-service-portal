'use client'

import { useEnrollmentSeason } from '@/lib/useEnrollmentSeason'
import { ClosedPage } from './ClosedPage'
import { LandingPage } from './LandingPage'

/**
 * The landing screen for the active season.
 *
 * Open and closed are two pages rather than one page with different words: one
 * invites a family into an application, the other reports on a season that has
 * already ended. `/closed` still serves the post-season page directly, so content
 * reviewers can open it while the season is running.
 */
export function LandingRoute() {
  const { season } = useEnrollmentSeason()

  return season === 'closed' ? <ClosedPage /> : <LandingPage />
}
