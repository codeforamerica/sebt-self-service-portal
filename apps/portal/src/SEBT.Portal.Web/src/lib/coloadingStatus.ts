/**
 * Classifies the user's household into one of four buckets used by analytics
 * (DC-215). Mutually exclusive:
 *
 *   unknown           — auth state hasn't resolved yet (`isCoLoaded` is null/undefined).
 *                       Filterable downstream so we don't bias any of the three
 *                       resolved buckets with "auth-not-yet-loaded" pageviews.
 *   non_co_loaded     — auth resolved, user did NOT match into SNAP/TANF.
 *   co_loaded_only    — matched into SNAP/TANF only; no SummerEbt cases or applications.
 *   mixed_eligibility — co-loaded user who also has at least one non-co-loaded
 *                       benefit (a SummerEbt case or a submitted application).
 *
 * `isCoLoaded` is the JWT-derived auth claim from `useAuth().session?.isCoLoaded`,
 * not `data.benefitIssuanceType` — they can disagree, and the auth claim is the
 * source of truth for "did the user match into SNAP/TANF at all."
 */

import type { HouseholdData } from '@/features/household'

export type ColoadingStatus = 'unknown' | 'non_co_loaded' | 'co_loaded_only' | 'mixed_eligibility'

export function getColoadingStatus(
  isCoLoaded: boolean | null | undefined,
  household: Pick<HouseholdData, 'summerEbtCases' | 'applications'>
): ColoadingStatus {
  if (isCoLoaded == null) return 'unknown'
  if (!isCoLoaded) return 'non_co_loaded'

  const hasSummerEbtCase = household.summerEbtCases.some((c) => c.issuanceType === 'SummerEbt')
  const hasApplication = household.applications.length > 0

  if (hasSummerEbtCase || hasApplication) return 'mixed_eligibility'
  return 'co_loaded_only'
}
