/**
 * State-Specific External Links Configuration
 *
 * Centralized configuration for all external links used across the application.
 * Each state can have its own set of URLs for footer, help section, and other links.
 */

import { assertKnownStateCode, type StateCode } from './state'

export interface LinkItem {
  key: string
  href: string
  translationKey: string
  icon?: string
}

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

/**
 * State-specific link configurations
 */
const stateLinks: Record<StateCode, StateLinks> = {
  dc: {
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
  co: {
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
  }
}

/**
 * Get links configuration for a specific state. Throws for a code that is not registered.
 * @param state - Two-letter state code (e.g., 'dc', 'co')
 * @returns State-specific links
 */
export function getStateLinks(state: StateCode): StateLinks {
  assertKnownStateCode(state)
  // eslint-disable-next-line security/detect-object-injection -- validated as an own key above
  return stateLinks[state]
}

/**
 * Footer links with translation keys for iteration
 */
export function getFooterLinks(state: StateCode): LinkItem[] {
  const links = getStateLinks(state)
  return [
    { key: 'accessibility', href: links.footer.accessibility, translationKey: 'linkAccessibility' },
    {
      key: 'privacyAndSecurity',
      href: links.footer.privacyAndSecurity,
      translationKey: 'linkPrivacyPolicy'
    },
    {
      key: 'googleTranslateDisclaimer',
      href: links.footer.googleTranslateDisclaimer,
      translationKey: 'linkGoogleTranslate'
    },
    { key: 'about', href: links.footer.about, translationKey: 'linkAbout' },
    {
      key: 'termsAndConditions',
      href: links.footer.termsAndConditions,
      translationKey: 'linkTerms'
    }
  ]
}

/**
 * Help section links with icons for iteration
 */
export function getHelpLinks(state: StateCode): LinkItem[] {
  const links = getStateLinks(state)
  return [
    { key: 'faqs', href: links.help.faqs, translationKey: 'linkFaqs', icon: 'faqs-icon.svg' },
    {
      key: 'contactUs',
      href: links.help.contactUs,
      translationKey: 'linkContactUs',
      icon: 'contact-icon.svg'
    }
  ]
}
