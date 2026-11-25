import { getState, type StateCode } from '@sebt/design-system/src/lib/state'

import type { CheckerAsset } from './checkerAssetPath'
import type { FlowConfig } from './flowConfig'
import type { LandingConfig } from './landingConfig'

// One entry per state: flow shape, landing structure, and artwork. Add a state by adding an
// entry here and dropping files into `public/images/states/{state}/`; no component changes.

interface CheckerStateConfig {
  flow: FlowConfig
  landing: LandingConfig
  /**
   * Slots are optional: a state that omits one renders no image rather than borrowing
   * another state's. Card icons are decorative, so omitting one costs nothing semantically.
   */
  assets: Partial<Record<CheckerAsset, string>>
}

const checkerStateConfigs: Record<StateCode, CheckerStateConfig> = {
  dc: {
    flow: { useReviewStep: false, distinguishNoResults: false, resultsLayout: 'singleOutcome' },
    landing: { useAccordion: false },
    assets: {
      // No landingLogo — DC's landing page goes straight from toolbar to <h1>.
      formCard: 'icon-form-card.svg',
      reviewCard: 'icon-review-card.svg',
      resultsEnrolled: 'icon-checkmark-card.svg',
      // A not-enrolled result is an invitation to apply, not a warning, so it
      // carries the review artwork. The alert artwork is reserved for the error
      // screen, where something actually went wrong.
      resultsNotEnrolled: 'icon-review-card.svg',
      // Once the season has closed there is nothing left to invite, so the same
      // outcome becomes a warning and takes the alert artwork.
      resultsNotEnrolledClosed: 'icon-alert-card.svg',
      errorCard: 'icon-alert-card.svg'
    }
  },
  co: {
    flow: { useReviewStep: true, distinguishNoResults: true, resultsLayout: 'household' },
    landing: { useAccordion: true },
    assets: {
      landingLogo: 'summer-ebt-logo.svg',
      formCard: 'icon-form-card.svg',
      reviewCard: 'icon-review-card.svg',
      resultsEnrolled: 'icon-review-card.svg',
      resultsNotEnrolled: 'icon-alert-card.svg',
      resultsNotEnrolledClosed: 'icon-alert-card.svg',
      errorCard: 'icon-alert-card.svg'
    }
  }
}

/** Configured states, so tests can iterate them at runtime. */
export const CHECKER_STATES = Object.keys(checkerStateConfigs) as StateCode[]

/** Registry entry for the active state. */
export function getCheckerStateConfig(): CheckerStateConfig {
  return checkerStateConfigs[getState()]
}
