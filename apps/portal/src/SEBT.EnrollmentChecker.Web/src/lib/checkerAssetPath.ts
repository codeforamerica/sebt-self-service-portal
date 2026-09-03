import { getState, getStateAssetPath, type StateCode } from '@sebt/design-system/src/lib/state'

// Checker artwork, resolved per state. Wraps the design system's
// `getStateAssetPath`, which has no base-path notion because the portal is never
// served under one.
//
// Reads `process.env` directly rather than `./env`: @t3-oss/env-nextjs freezes
// its values at module init, so they can't be stubbed in tests. Next.js still
// inlines literal `process.env.NEXT_PUBLIC_*` accesses at build time.

/**
 * A place in the UI that shows state-specific artwork.
 *
 * Named for where it appears, not the file behind it — states map outcomes to
 * artwork differently (CO opens an enrolled result with the review card, DC with
 * a checkmark).
 */
export const CHECKER_ASSETS = [
  'landingLogo',
  'formCard',
  'reviewCard',
  'resultsEnrolled',
  'resultsNotEnrolled',
  'errorCard'
] as const

export type CheckerAsset = (typeof CHECKER_ASSETS)[number]

/**
 * Add a state by adding an entry and dropping files into
 * `public/images/states/{state}/`. No component changes needed.
 *
 * Slots are optional: a state that omits one renders no image rather than
 * borrowing another state's. Card icons are decorative, so omitting one costs
 * nothing semantically.
 */
const checkerAssets: Record<StateCode, Partial<Record<CheckerAsset, string>>> = {
  dc: {
    // No landingLogo — DC's landing page goes straight from toolbar to <h1>.
    formCard: 'icon-form-card.svg',
    reviewCard: 'icon-review-card.svg',
    resultsEnrolled: 'icon-checkmark-card.svg',
    // DC's not-enrolled result reuses the review artwork.
    resultsNotEnrolled: 'icon-review-card.svg'
    // No errorCard — DC has no alert artwork.
  },
  co: {
    landingLogo: 'summer-ebt-logo.svg',
    formCard: 'icon-form-card.svg',
    reviewCard: 'icon-review-card.svg',
    resultsEnrolled: 'icon-review-card.svg',
    resultsNotEnrolled: 'icon-alert-card.svg',
    errorCard: 'icon-alert-card.svg'
  }
}

/** Configured states, so tests can iterate them at runtime. */
export const CHECKER_STATES = Object.keys(checkerAssets) as StateCode[]

/**
 * Public URL for an asset in the active state.
 *
 * @returns the path, or `undefined` when the state has no artwork for the slot.
 */
export function getCheckerAssetPath(asset: CheckerAsset): string | undefined {
  const state = getState()

  // eslint-disable-next-line security/detect-object-injection -- state is typed StateCode
  const fileName = checkerAssets[state]?.[asset]
  if (!fileName) {
    return undefined
  }

  // Strip trailing slashes so "/checker/" and "/checker" both yield one separator.
  const basePath = (process.env.NEXT_PUBLIC_BASE_PATH ?? '').replace(/\/+$/, '')

  return `${basePath}${getStateAssetPath(state, fileName)}`
}
