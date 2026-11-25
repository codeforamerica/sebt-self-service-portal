/**
 * State Configuration Registry
 *
 * Centralized state resolution and configuration for multi-state deployment.
 * All state-specific metadata lives here — add a new state by adding an entry
 * to `stateOverrides`. No inline conditionals needed in components.
 */

/**
 * Supported state codes — add new states here. The single list: the apps' env schemas
 * and the design-token scripts import it, and `stateOverrides` must cover every entry.
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

/** An image the portal renders: where it lives and the size it is laid out at. */
interface ImageAsset {
  src: string
  width: number
  height: number
}

interface StateAssets {
  /** Header logo; dimensions match the SVG viewBox so it renders at its natural aspect ratio */
  logo: ImageAsset
  /** Footer seal */
  seal: ImageAsset
  icons: {
    /** Language selector glyph */
    translate: string
    /** FAQs tile in the help section */
    faqs: string
    /** Contact tile in the help section */
    contact: string
  }
}

/** External links, per state, for the footer, help section, and other pages. */
export interface StateLinks {
  /** Footer navigation links */
  footer: {
    publicNotifications: string
    accessibility: string
    privacyAndSecurity: string
    googleTranslateDisclaimer: string
    about: string
    termsAndConditions: string
    /** CO-specific: Colorado Transparency Online Project */
    transparencyOnline?: string
    /** CO-specific: Colorado General Notices page */
    generalNotices?: string
    /** CO-specific: Digital Accessibility Statement */
    digitalAccessibility?: string
  }
  /** Help section links */
  help: {
    faqs: string
    contactUs: string
    /** State's main SEBT website; shown verbatim in maintenance-page button labels */
    sebtMainSite: string
    /** CO-specific: Help desk email (mailto: link) */
    helpDeskEmail?: string
  }
  /** Other external links used throughout the app */
  external: {
    contactUsAssistance: string
  }
}

/** Maintenance page action buttons: 'stacked' is a column with a full-width outline button, 'row' keeps both at content width side by side. */
type MaintenanceActionLayout = 'stacked' | 'row'

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
  links: StateLinks
  maintenanceActionLayout: MaintenanceActionLayout
  assets: StateAssets
}

/**
 * What a state must declare itself: everything without a state-agnostic default, so a new state
 * that omits identity, links or behavior values fails to compile. Shared values are optional to override.
 */
interface StateOverride extends Omit<StateConfig, 'assets' | 'maintenanceActionLayout'> {
  maintenanceActionLayout?: MaintenanceActionLayout
  assets: Pick<StateAssets, 'logo' | 'seal'> & {
    icons?: Partial<StateAssets['icons']>
  }
}

/**
 * Values every state shares unless it overrides them. A value belongs here only when all states
 * have the same one; anything that differs, and every identity value, lives in `stateOverrides`.
 */
const sharedDefaults: Pick<StateConfig, 'maintenanceActionLayout'> & {
  icons: StateAssets['icons']
} = {
  maintenanceActionLayout: 'row',
  icons: {
    translate: '/images/shared/icons/translate_Rounded.svg',
    faqs: '/images/shared/icons/faqs-icon.svg',
    contact: '/images/shared/icons/contact-icon.svg'
  }
}

/** Public URL of a file in a state's own image folder. */
export function getStateAssetPath(state: StateCode, assetPath: string): string {
  return `/images/states/${state}/${assetPath}`
}

/**
 * Per-state registry — add new states here. The record type makes a missing state a compile error.
 */
const stateOverrides: Record<StateCode, StateOverride> = {
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
    pageTitleText: 'text-ink',
    links: {
      footer: {
        publicNotifications: 'https://sunbucks.dc.gov/page/public-notifications',
        accessibility: 'https://dc.gov/node/298582',
        privacyAndSecurity: 'https://dc.gov/node/298592',
        googleTranslateDisclaimer: 'https://dc.gov/page/google-translate-disclaimer',
        about: 'https://dc.gov/node/819092',
        termsAndConditions: 'https://dc.gov/node/900572'
      },
      help: {
        faqs: 'https://sunbucks.dc.gov/page/sun-bucks-frequently-asked-questions',
        contactUs: 'https://sunbucks.dc.gov/page/contact-us',
        sebtMainSite: 'https://sunbucks.dc.gov'
      },
      external: {
        contactUsAssistance: 'https://sunbucks.dc.gov/page/contact-us'
      }
    },
    assets: {
      logo: {
        src: getStateAssetPath('dc', 'logo.svg'),
        width: 122,
        height: 52
      },
      seal: {
        src: getStateAssetPath('dc', 'seal.svg'),
        width: 121,
        height: 51
      }
    }
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
    pageTitleText: 'text-primary',
    maintenanceActionLayout: 'stacked',
    links: {
      footer: {
        publicNotifications: '#',
        accessibility: '#',
        privacyAndSecurity: '#',
        googleTranslateDisclaimer: '#',
        about: '#',
        termsAndConditions: '#',
        transparencyOnline: 'https://data.colorado.gov/stories/s/fjyf-bdat',
        generalNotices: 'https://www.colorado.gov/general-notices',
        digitalAccessibility: 'https://cdhs.colorado.gov/accessibility-at-cdhs'
      },
      help: {
        faqs: 'https://cdhs.colorado.gov/summer-ebt-faq',
        contactUs: 'mailto:cdhs_sebt_supportcenter@state.co.us',
        sebtMainSite: 'https://cdhs.colorado.gov/summer-ebt',
        helpDeskEmail: 'mailto:cdhs_sebt_supportcenter@state.co.us'
      },
      external: {
        contactUsAssistance: 'https://mycolorado.state.co.us/customer-support'
      }
    },
    assets: {
      logo: {
        src: getStateAssetPath('co', 'logo.svg'),
        width: 192,
        height: 28
      },
      seal: {
        src: getStateAssetPath('co', 'seal.svg'),
        width: 136,
        height: 28
      },
      icons: {
        translate: getStateAssetPath('co', 'icons/translate_Rounded.svg')
      }
    }
  }
}

function withSharedDefaults(override: StateOverride): StateConfig {
  return {
    ...override,
    maintenanceActionLayout:
      override.maintenanceActionLayout ?? sharedDefaults.maintenanceActionLayout,
    assets: {
      ...override.assets,
      icons: { ...sharedDefaults.icons, ...override.assets.icons }
    }
  }
}

/** Each state's overrides over the shared values. Components read it via getStateConfig(). */
const stateConfigs = Object.fromEntries(
  Object.entries(stateOverrides).map(([code, override]) => [code, withSharedDefaults(override)])
) as Record<StateCode, StateConfig>

function isKnownStateCode(value: string): value is StateCode {
  return Object.prototype.hasOwnProperty.call(stateConfigs, value)
}

/** Throws for a code that is not in the registry, so every per-state lookup fails the same way. */
export function assertKnownStateCode(value: string): asserts value is StateCode {
  if (!isKnownStateCode(value)) {
    throw new Error(`Unknown state code: ${value}`)
  }
}

function toStateCode(raw: string): StateCode {
  const code = raw.toLowerCase()
  if (!isKnownStateCode(code)) {
    throw new Error(`Unknown state code: ${raw}`)
  }
  return code
}

/**
 * Get the full configuration for a state. Throws for a code that is not registered.
 */
export function getStateConfig(state: StateCode): StateConfig {
  assertKnownStateCode(state)
  // eslint-disable-next-line security/detect-object-injection -- validated as an own key above
  return stateConfigs[state]
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
      return toStateCode(fromDocument)
    }
  }
  const fromEnv = process.env.STATE || process.env.NEXT_PUBLIC_STATE
  // Absent on purpose during the state-agnostic image build; a wrong value never is.
  return fromEnv ? toStateCode(fromEnv) : 'dc'
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
