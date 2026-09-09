import { getSiteDisplayName, type StateCode } from '@sebt/design-system/src/lib/state'
import type { Metadata } from 'next'

/**
 * Build the Enrollment Checker's root metadata for a given state.
 *
 * The program name in the title and description is state-specific: the District
 * of Columbia brands Summer EBT as "SUN Bucks", while Colorado uses "Summer EBT"
 * and never the "SUN Bucks" name. `getSiteDisplayName` is the single source of
 * truth for that branded, per-state name (e.g. "District of Columbia SUN Bucks",
 * "Colorado Summer EBT"), so the right string renders for whichever state this
 * deployment is built for.
 *
 * Kept as a pure `state -> Metadata` function so it can be unit-tested for every
 * state without evaluating the root layout's Server Component module.
 */
export function buildRootMetadata(state: StateCode): Metadata {
  const siteDisplayName = getSiteDisplayName(state)

  return {
    title: {
      default: `${siteDisplayName} Enrollment Checker`,
      template: `%s | ${siteDisplayName}`
    },
    description: `Check if your child is already enrolled in ${siteDisplayName}.`,
    robots: { index: false, follow: false }
  }
}
