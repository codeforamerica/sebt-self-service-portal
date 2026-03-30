/**
 * State Configuration Registry
 *
 * Centralized state resolution and configuration for multi-state deployment.
 * All state-specific metadata lives here — add a new state by adding an entry
 * to `stateConfigs`. No inline conditionals needed in components.
 */

/** Supported state codes — add new states here */
export type StateCode = 'dc' | 'co'

export interface StateConfig {
  /** Full display name (e.g., 'District of Columbia') */
  name: string
  /** State-specific program name (e.g., 'DC SUN Bucks', 'Summer EBT') */
  programName: string
  /** Alt text for the state seal image in the footer */
  sealAlt: string
  /** Extra CSS classes appended to the mobile language selector button */
  languageSelectorClass?: string
  /** Extra CSS classes appended to the mobile language submenu */
  languageSubmenuClass?: string
  /** USWDS background utility class for action buttons */
  actionButtonBg: string
  /** USWDS text color utility class for action buttons */
  actionButtonText: string
}

/**
 * State configuration registry — add new states here.
 * Components use getStateConfig() to access state-specific values.
 */
const stateConfigs: Record<StateCode, StateConfig> = {
  dc: {
    name: 'District of Columbia',
    programName: 'DC SUN Bucks',
    sealAlt: 'Government of the District of Columbia - Muriel Bowser, Mayor',
    actionButtonBg: 'bg-secondary',
    actionButtonText: 'text-ink'
  },
  co: {
    name: 'Colorado',
    programName: 'Summer EBT',
    sealAlt: 'Colorado Official State Web Portal',
    languageSelectorClass: 'border-primary radius-md text-primary',
    languageSubmenuClass: 'bg-primary-dark',
    actionButtonBg: 'bg-primary',
    actionButtonText: 'text-white'
  }
}

const defaultConfig: StateConfig = stateConfigs.dc as StateConfig

/**
 * Get the full configuration for a state
 */
export function getStateConfig(state: StateCode): StateConfig {
  // eslint-disable-next-line security/detect-object-injection -- state is StateCode union
  return stateConfigs[state] ?? defaultConfig
}

/**
 * Get the current state code from environment
 * @returns Two-letter state code (e.g., 'dc', 'co')
 */
export function getState(): StateCode {
  return (process.env.NEXT_PUBLIC_STATE || 'dc').toLowerCase() as StateCode
}

/**
 * Get state display name
 */
export function getStateName(state: StateCode): string {
  return getStateConfig(state).name
}

/**
 * Get state-specific asset path
 */
export function getStateAssetPath(state: StateCode, assetPath: string): string {
  return `/images/states/${state}/${assetPath}`
}
