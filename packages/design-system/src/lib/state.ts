/**
 * State Configuration Registry
 *
 * Centralized state resolution and configuration for multi-state deployment.
 * All state-specific metadata lives here — add a new state by adding an entry
 * to `stateConfigs`. No inline conditionals needed in components.
 */

/** Supported state codes — add new states here */
export type StateCode = 'dc' | 'co'

/**
 * Full set of language codes the design system knows how to render labels for.
 * The runtime list of languages offered to a user is per-state (see
 * `StateConfig.supportedLanguages`), but `languageNames` and
 * `languageTranslationKeys` cover every member of this union so a state that
 * offers a given language always has its native label available.
 */
export type SupportedLanguage = 'en' | 'es' | 'am'

export interface StateConfig {
  /** Full display name (e.g., 'District of Columbia') */
  name: string
  /** State-specific program name (e.g., 'DC SUN Bucks', 'Summer EBT') */
  programName: string
  /** Branded site name for page titles and link previews (e.g., 'DC SUN Bucks', 'Colorado Summer EBT') */
  siteDisplayName: string
  /** Meta description for page titles, Open Graph, and link previews */
  portalMetadataDescription: string
  /** Alt text for the state seal image in the footer */
  sealAlt: string
  /** Languages offered in this state's UI (drives the LanguageSelector and `?lang=` validation) */
  supportedLanguages: readonly SupportedLanguage[]
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
    siteDisplayName: 'DC SUN Bucks',
    portalMetadataDescription:
      'Apply for Summer EBT (SUN Bucks) benefits in District of Columbia. Check eligibility, track your application status, and manage your benefits online.',
    sealAlt: 'Government of the District of Columbia - Muriel Bowser, Mayor',
    supportedLanguages: ['en', 'es', 'am'],
    actionButtonBg: 'bg-secondary',
    actionButtonText: 'text-ink'
  },
  co: {
    name: 'Colorado',
    programName: 'Summer EBT',
    siteDisplayName: 'Colorado Summer EBT',
    portalMetadataDescription: 'Manage your CO Summer EBT benefits online.',
    sealAlt: 'Colorado Official State Web Portal',
    supportedLanguages: ['en', 'es'],
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
 * Branded site name for page titles, Open Graph, and link previews.
 */
export function getSiteDisplayName(state: StateCode): string {
  return getStateConfig(state).siteDisplayName
}

/**
 * Meta description for page titles, Open Graph, and link previews.
 */
export function getPortalMetadataDescription(state: StateCode): string {
  return getStateConfig(state).portalMetadataDescription
}

/**
 * Get state-specific asset path
 */
export function getStateAssetPath(state: StateCode, assetPath: string): string {
  return `/images/states/${state}/${assetPath}`
}
