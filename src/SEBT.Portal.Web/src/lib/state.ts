/**
 * State Configuration Utility
 *
 * Centralized state resolution for multi-state deployment.
 * Use this instead of duplicating process.env.NEXT_PUBLIC_STATE checks.
 */

/**
 * Get the current state code from environment
 * @returns Two-letter state code (e.g., 'dc', 'co')
 */
export function getState(): string {
  return process.env.NEXT_PUBLIC_STATE || 'dc'
}

/**
 * Get state display name
 * @param state - Two-letter state code
 * @returns Full state name or formatted code
 */
export function getStateName(state: string): string {
  switch (state) {
    case 'dc':
      return 'District of Columbia'
    case 'co':
      return 'Colorado'
    default:
      return `State of ${state.toUpperCase()}`
  }
}

/**
 * Get state-specific asset path
 * @param state - Two-letter state code
 * @param assetPath - Path within state folder (e.g., 'icons/logo.svg')
 * @returns Full path to state-specific asset
 */
export function getStateAssetPath(state: string, assetPath: string): string {
  return `/images/states/${state}/${assetPath}`
}
