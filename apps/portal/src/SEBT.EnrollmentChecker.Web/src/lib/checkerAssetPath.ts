import { getState, getStateAssetPath, type StateCode } from '@sebt/design-system/src/lib/state'

// Checker-specific artwork, resolved per state.
//
// The design system's `getStateAssetPath` has no base-path notion because the
// portal is never served under one; the checker can be (NEXT_PUBLIC_BASE_PATH),
// so this wrapper composes the two rather than bending the shared helper.
//
// `process.env` is read directly rather than through `./env`: @t3-oss/env-nextjs
// freezes its values at module init, which makes them unstubbable in tests.
// `getState` reads process.env for the same reason. Next.js still inlines these
// literal `process.env.NEXT_PUBLIC_*` member accesses at build time.

/**
 * A place in the checker UI that shows state-specific artwork.
 *
 * Slots are named for where they appear, not for the file behind them, because
 * the outcome-to-artwork mapping is itself state-specific: Colorado leads an
 * enrolled result with the review card while DC uses a checkmark. Keying on the
 * slot lets each state answer "what belongs here?" independently.
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
 * Per-state artwork registry — add a new state by adding an entry, and drop its
 * files into `public/images/states/{state}/`. No component changes needed.
 *
 * Slots are deliberately optional. A state that omits one has no artwork for
 * that screen, and callers render no image rather than borrowing another
 * state's. Every card icon is decorative (`alt=""`, `aria-hidden`), so omitting
 * one costs nothing semantically.
 */
const checkerAssets: Record<StateCode, Partial<Record<CheckerAsset, string>>> = {
  dc: {
    // No landingLogo: DC's landing page goes straight from the toolbar to the
    // <h1>, with branding carried by the toolbar logo.
    formCard: 'icon-form-card.svg',
    reviewCard: 'icon-review-card.svg',
    resultsEnrolled: 'icon-checkmark-card.svg',
    // DC's not-enrolled result is the "Apply for DC Sun Bucks" screen, which
    // uses the same review/apply artwork as the confirm step.
    resultsNotEnrolled: 'icon-review-card.svg'
    // No errorCard: DC has no alert artwork yet (DC-727 open question).
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

/**
 * Every state the checker has artwork for. `checkerAssets` is keyed by the
 * design system's `StateCode`, so TypeScript already guarantees this covers all
 * of them — it exists so tests can iterate states at runtime.
 */
export const CHECKER_STATES = Object.keys(checkerAssets) as StateCode[]

/**
 * Resolve the public URL for a checker asset in the active state.
 *
 * @returns the path, or `undefined` when this state has no artwork for the slot
 *          — callers should render no image in that case.
 */
export function getCheckerAssetPath(asset: CheckerAsset): string | undefined {
  const state = getState()

  // eslint-disable-next-line security/detect-object-injection -- state is typed StateCode
  const fileName = checkerAssets[state]?.[asset]
  if (!fileName) {
    return undefined
  }

  // Trailing slashes are stripped so a base path of "/checker/" and "/checker"
  // both produce a single separator against the leading slash of the asset path.
  const basePath = (process.env.NEXT_PUBLIC_BASE_PATH ?? '').replace(/\/+$/, '')

  return `${basePath}${getStateAssetPath(state, fileName)}`
}
