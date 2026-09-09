'use client'

import { useCheckerFeatures } from '@/features/maintenance/hooks/useCheckerFeatures'
import { getEnrollmentConfig } from './stateConfig'

/**
 * Which season the checker speaks in. `closed` answers "was your student enrolled?"
 * in the past tense and carries no apply paths.
 */
export type EnrollmentSeason = 'open' | 'closed'

export interface EnrollmentSeasonState {
  season: EnrollmentSeason
  /**
   * True while the first features poll is still in flight and nothing has failed yet.
   *
   * The season decides what a page *is*, not just what it says, so callers hold their
   * content rather than render one season's screen and swap it for the other's. The
   * flag clears on the first failure, so a checker whose features endpoint is down
   * falls through to the open season instead of holding an empty page.
   */
  isResolving: boolean
}

/**
 * The active season, from the `enable_enrollment` flag on the features poll.
 *
 * Fails open: a failed poll, or an API that predates the field, leaves the checker on
 * its open-season copy. The opposite default would let one bad request tell families
 * enrollment had ended.
 *
 * `useApplyHref` fails closed on that same poll, so a failure shows open-season copy
 * with no apply link anywhere. That pairing is chosen, not accidental: the check still
 * runs and the paper-application note still stands, which beats naming the wrong season
 * or publishing a dead link.
 */
export function useEnrollmentSeason(): EnrollmentSeasonState {
  const { apiBaseUrl } = getEnrollmentConfig()
  const { data, isPending, failureCount } = useCheckerFeatures(apiBaseUrl)

  return {
    season: data?.enrollment?.enabled === false ? 'closed' : 'open',
    isResolving: isPending && failureCount === 0
  }
}
