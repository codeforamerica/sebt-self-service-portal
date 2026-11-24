/**
 * External link helpers. Each state's URLs live in its `links` entry in StateConfig (state.ts).
 */

import { getStateConfig, type StateCode, type StateLinks } from './state'

export interface LinkItem {
  key: string
  href: string
  translationKey: string
  /** Public URL of the link's icon */
  icon?: string
}

/**
 * Get links configuration for a specific state. Throws for a code that is not registered.
 * @param state - Two-letter state code (e.g., 'dc', 'co')
 * @returns State-specific links
 */
export function getStateLinks(state: StateCode): StateLinks {
  return getStateConfig(state).links
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
export function getHelpLinks(state: StateCode): Array<LinkItem & { icon: string }> {
  const links = getStateLinks(state)
  const { icons } = getStateConfig(state).assets
  return [
    { key: 'faqs', href: links.help.faqs, translationKey: 'linkFaqs', icon: icons.faqs },
    {
      key: 'contactUs',
      href: links.help.contactUs,
      translationKey: 'linkContactUs',
      icon: icons.contact
    }
  ]
}
