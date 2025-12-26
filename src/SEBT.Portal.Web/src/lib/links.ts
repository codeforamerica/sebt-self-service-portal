/**
 * State-Specific External Links Configuration
 *
 * Centralized configuration for all external links used across the application.
 * Each state can have its own set of URLs for footer, help section, and other links.
 */

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
  }
  /** Help section links */
  help: {
    faqs: string
    contactUs: string
  }
  /** Other external links used throughout the app */
  external: {
    contactUsAssistance: string
  }
}

/**
 * State-specific link configurations
 */
const stateLinks: Record<string, StateLinks> = {
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
      contactUs: 'https://sunbucks.dc.gov/page/contact-us'
    },
    external: {
      contactUsAssistance: 'https://sunbucks.dc.gov/page/contact-us'
    }
  }
  // Add more states as needed
  // co: {
  //   footer: { ... },
  //   help: { ... },
  //   external: { ... },
  // },
}

/**
 * Default links used when a state-specific configuration is not available
 */
const defaultLinks: StateLinks = stateLinks.dc as StateLinks

/**
 * Get links configuration for a specific state
 * @param state - Two-letter state code (e.g., 'dc', 'co')
 * @returns State-specific links or default links if state not configured
 */
export function getStateLinks(state: string): StateLinks {
  return stateLinks[state.toLowerCase()] || defaultLinks
}

/**
 * Footer links with translation keys for iteration
 */
export function getFooterLinks(state: string): LinkItem[] {
  const links = getStateLinks(state)
  return [
    { key: 'accessibility', href: links.footer.accessibility, translationKey: 'accessibility' },
    {
      key: 'privacyAndSecurity',
      href: links.footer.privacyAndSecurity,
      translationKey: 'privacyAndSecurity'
    },
    {
      key: 'googleTranslateDisclaimer',
      href: links.footer.googleTranslateDisclaimer,
      translationKey: 'googleTranslateDisclaimer'
    },
    { key: 'about', href: links.footer.about, translationKey: 'about' },
    {
      key: 'termsAndConditions',
      href: links.footer.termsAndConditions,
      translationKey: 'termsAndConditions'
    }
  ]
}

/**
 * Help section links with icons for iteration
 */
export function getHelpLinks(state: string): LinkItem[] {
  const links = getStateLinks(state)
  return [
    { key: 'faqs', href: links.help.faqs, translationKey: 'faqs', icon: 'faqs-icon.svg' },
    {
      key: 'contactUs',
      href: links.help.contactUs,
      translationKey: 'contactUs',
      icon: 'contact-icon.svg'
    }
  ]
}
