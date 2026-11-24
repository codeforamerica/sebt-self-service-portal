import { getState, getStateAssetPath } from '@sebt/design-system/src/lib/state'

import { getCheckerStateConfig } from './checkerStateConfig'

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
  'resultsNotEnrolledClosed',
  'errorCard'
] as const

export type CheckerAsset = (typeof CHECKER_ASSETS)[number]

/**
 * Public URL for an asset in the active state.
 *
 * @returns the path, or `undefined` when the state has no artwork for the slot.
 */
export function getCheckerAssetPath(asset: CheckerAsset): string | undefined {
  const state = getState()

  // eslint-disable-next-line security/detect-object-injection -- asset is typed CheckerAsset
  const fileName = getCheckerStateConfig().assets[asset]
  if (!fileName) {
    return undefined
  }

  // Strip trailing slashes so "/checker/" and "/checker" both yield one separator.
  const basePath = (process.env.NEXT_PUBLIC_BASE_PATH ?? '').replace(/\/+$/, '')

  return `${basePath}${getStateAssetPath(state, fileName)}`
}
