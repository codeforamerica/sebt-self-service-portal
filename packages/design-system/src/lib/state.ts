/**
 * State Configuration Registry
 *
 * Centralized state resolution and configuration for multi-state deployment.
 * All state-specific metadata lives here — add a new state by adding an entry
 * to `stateConfigs`. No inline conditionals needed in components.
 */

/**
 * Supported state codes — add new states here. The single list: the apps' env schemas
 * and the design-token scripts import it, and `stateConfigs` must cover every entry.
 */
export const STATE_CODES = ['dc', 'co'] as const

export type StateCode = (typeof STATE_CODES)[number]

/** How a guardian signs in: email one-time code, or an OpenID Connect identity provider. */
export type LoginMode = 'otp' | 'oidc'

/**
 * Full set of language codes the design system knows how to render labels for.
 * The runtime list of languages offered to a user is per-state (see
 * `StateConfig.supportedLanguages`), but `languageNames` and
 * `languageTranslationKeys` cover every member of this union so a state that
 * offers a given language always has its native label available.
 */
export type SupportedLanguage = 'en' | 'es' | 'am'

/** How the portal names an EBT card to the guardian: by the child it belongs to, or by its last four digits. */
type CardIdentifier = 'childName' | 'cardLastFour'

/** What the address-not-found screen offers besides editing: keep the entered address, or contact the program. */
type AddressNotFoundAction = 'useEnteredAddress' | 'contactUs'

interface AddressSettings {
  /** City pre-filled on an empty address form */
  defaultCity: string
  /** USPS state code pre-selected on an empty address form */
  defaultState: string
  /** Shows the street-address helper hint under the first street line */
  showStreetAddressHint: boolean
  notFoundAction: AddressNotFoundAction
  /** Shows the notice explaining that the suggested address uses USPS abbreviations */
  showAbbreviatedNotice: boolean
  /**
   * Smarty `prefer_states` value. Semicolon-delimited: the embedded key's dashboard config trips
   * a validator on comma-delimited `*_states` input, and Smarty parses semicolons equivalently.
   */
  autocompletePreferStates: string
}

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
  /** Sign-in mechanism; also selects the OIDC step-up gate */
  loginMode: LoginMode
  cardIdentifier: CardIdentifier
  /** Benefits can load onto an existing SNAP/TANF card, which enables the co-loaded info screens */
  hasCoLoadedBenefits: boolean
  address: AddressSettings
  /**
   * Shows a "please wait" interstitial while auth status, the sign-in callback, or household
   * data is in flight. States without it keep the blank screen those pages shipped with.
   */
  showLoadingInterstitial: boolean
  /** Extra CSS classes appended to the mobile language selector button */
  languageSelectorClass?: string
  /** Extra CSS classes appended to the mobile language submenu */
  languageSubmenuClass?: string
  /** USWDS background utility class for action buttons */
  actionButtonBg: string
  /** USWDS text color utility class for action buttons */
  actionButtonText: string
  /** USWDS text color utility class for the `h1` that titles a page */
  pageTitleText: string
}

/**
 * State configuration registry — add new states here.
 * Components use getStateConfig() to access state-specific values.
 */
const stateConfigs: Record<StateCode, StateConfig> = {
  dc: {
    name: 'District of Columbia',
    programName: 'DC SUN Bucks',
    siteDisplayName: 'District of Columbia SUN Bucks',
    portalMetadataDescription:
      'Manage your DC SUN Bucks (Summer EBT) benefits online. Check enrollment, benefit expiration, and EBT card status for your students.',
    sealAlt: 'Government of the District of Columbia - Muriel Bowser, Mayor',
    supportedLanguages: ['en', 'es', 'am'],
    loginMode: 'otp',
    cardIdentifier: 'childName',
    hasCoLoadedBenefits: true,
    address: {
      defaultCity: 'Washington',
      defaultState: 'DC',
      showStreetAddressHint: true,
      notFoundAction: 'contactUs',
      showAbbreviatedNotice: true,
      autocompletePreferStates: 'DC;VA;MD'
    },
    showLoadingInterstitial: false,
    actionButtonBg: 'bg-secondary',
    actionButtonText: 'text-ink',
    pageTitleText: 'text-ink'
  },
  co: {
    name: 'Colorado',
    programName: 'Summer EBT',
    siteDisplayName: 'Colorado Summer EBT',
    portalMetadataDescription: 'Manage your CO Summer EBT benefits online.',
    sealAlt: 'Colorado Official State Web Portal',
    supportedLanguages: ['en', 'es'],
    loginMode: 'oidc',
    cardIdentifier: 'cardLastFour',
    hasCoLoadedBenefits: false,
    address: {
      defaultCity: '',
      defaultState: 'CO',
      showStreetAddressHint: false,
      notFoundAction: 'useEnteredAddress',
      showAbbreviatedNotice: false,
      autocompletePreferStates: 'CO'
    },
    showLoadingInterstitial: true,
    languageSelectorClass: 'border-primary radius-md text-primary',
    languageSubmenuClass: 'bg-primary-dark',
    actionButtonBg: 'bg-primary',
    actionButtonText: 'text-white',
    pageTitleText: 'text-primary'
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
 * Get the current state code.
 *
 * Resolution order, and why:
 *  1. `<html data-state>` — the browser's only runtime source. The portal ships
 *     one artifact for every state, so the state cannot be inlined into the
 *     bundle; the server stamps this attribute per request and client components
 *     read it back. Server and client therefore agree, so hydration is stable.
 *  2. `process.env.STATE` — unprefixed, so Next leaves it in the server process
 *     rather than inlining it. This is what server components read.
 *  3. `process.env.NEXT_PUBLIC_STATE` — the per-state build path, still used by
 *     the enrollment checker, which deploys one static export per state.
 */
export function getState(): StateCode {
  if (typeof document !== 'undefined') {
    const fromDocument = document.documentElement.dataset.state
    if (fromDocument) {
      return fromDocument.toLowerCase() as StateCode
    }
  }
  return (process.env.STATE || process.env.NEXT_PUBLIC_STATE || 'dc').toLowerCase() as StateCode
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
