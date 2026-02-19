/**
 * State Configuration Registry
 *
 * Centralized state resolution and configuration for multi-state deployment.
 * All state-specific metadata lives here — add a new state by adding an entry
 * to `stateConfigs`. No inline conditionals needed in components.
 */

export interface StateConfig {
  /** Full display name (e.g., 'District of Columbia') */
  name: string
  /** Alt text for the state seal image in the footer */
  sealAlt: string
  /** Extra CSS classes appended to the mobile language selector button */
  languageSelectorClass?: string
  /** Extra CSS classes appended to the mobile language submenu */
  languageSubmenuClass?: string
}

/**
 * State configuration registry — add new states here.
 * Components use getStateConfig() to access state-specific values.
 */
const stateConfigs: Record<string, StateConfig> = {
  dc: {
    name: 'District of Columbia',
    sealAlt: 'Government of the District of Columbia - Muriel Bowser, Mayor'
  },
  co: {
    name: 'Colorado',
    sealAlt: 'Colorado Official State Web Portal',
    languageSelectorClass: 'border-primary radius-md text-primary',
    languageSubmenuClass: 'bg-primary-dark'
  }
}

const defaultConfig: StateConfig = stateConfigs.dc as StateConfig

/**
 * Get the full configuration for a state
 */
export function getStateConfig(state: string): StateConfig {
  return stateConfigs[state.toLowerCase()] || defaultConfig
}

/**
 * Get the current state code from environment
 * @returns Two-letter state code (e.g., 'dc', 'co')
 */
export function getState(): string {
  return process.env.NEXT_PUBLIC_STATE || 'dc'
}

/**
 * Get state display name
 */
export function getStateName(state: string): string {
  return getStateConfig(state).name
}

/**
 * Get state-specific asset path
 */
export function getStateAssetPath(state: string, assetPath: string): string {
  return `/images/states/${state}/${assetPath}`
}
