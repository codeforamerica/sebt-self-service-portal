import { describe, expect, it } from 'vitest'

import {
  getPortalMetadataDescription,
  getSiteDisplayName,
  getStateConfig,
  type StateCode
} from './state'

describe('getStateConfig — supportedLanguages', () => {
  it('omits Amharic from the CO config', () => {
    expect(getStateConfig('co').supportedLanguages).not.toContain('am')
  })

  it('exposes only English and Spanish for CO', () => {
    expect(getStateConfig('co').supportedLanguages).toEqual(['en', 'es'])
  })

  it('includes Amharic in the DC config', () => {
    expect(getStateConfig('dc').supportedLanguages).toContain('am')
  })

  it('exposes English, Spanish, and Amharic for DC', () => {
    expect(getStateConfig('dc').supportedLanguages).toEqual(['en', 'es', 'am'])
  })
})

describe('getSiteDisplayName', () => {
  it('returns Colorado Summer EBT for CO', () => {
    expect(getSiteDisplayName('co')).toBe('Colorado Summer EBT')
  })

  it('returns District of Columbia SUN Bucks for DC', () => {
    expect(getSiteDisplayName('dc')).toBe('District of Columbia SUN Bucks')
  })
})

describe('getPortalMetadataDescription', () => {
  it('describes benefit management for CO', () => {
    expect(getPortalMetadataDescription('co')).toBe('Manage your CO Summer EBT benefits online.')
  })

  it('describes benefit management for DC', () => {
    expect(getPortalMetadataDescription('dc')).toBe(
      'Manage your DC SUN Bucks (Summer EBT) benefits online. Check enrollment, benefit expiration, and EBT card status for your students.'
    )
  })

  it.each(['dc', 'co'] as const)('keeps SEO copy free of application references for %s', (state) => {
    expect(getPortalMetadataDescription(state).toLowerCase()).not.toMatch(/apply|application/)
  })
})

describe('getStateConfig — card identification', () => {
  it('identifies DC cards by child name', () => {
    expect(getStateConfig('dc').cardIdentifier).toBe('childName')
  })

  it('identifies CO cards by the last four card digits', () => {
    expect(getStateConfig('co').cardIdentifier).toBe('cardLastFour')
  })
})

describe('getStateConfig — co-loaded benefits', () => {
  it('enables co-loaded benefit screens for DC', () => {
    expect(getStateConfig('dc').hasCoLoadedBenefits).toBe(true)
  })

  it('disables co-loaded benefit screens for CO', () => {
    expect(getStateConfig('co').hasCoLoadedBenefits).toBe(false)
  })
})

describe('getStateConfig — address', () => {
  it('configures DC address behavior', () => {
    expect(getStateConfig('dc').address).toEqual({
      defaultCity: 'Washington',
      defaultState: 'DC',
      showStreetAddressHint: true,
      notFoundAction: 'contactUs',
      showAbbreviatedNotice: true,
      autocompletePreferStates: 'DC;VA;MD'
    })
  })

  it('configures CO address behavior', () => {
    expect(getStateConfig('co').address).toEqual({
      defaultCity: '',
      defaultState: 'CO',
      showStreetAddressHint: false,
      notFoundAction: 'useEnteredAddress',
      showAbbreviatedNotice: false,
      autocompletePreferStates: 'CO'
    })
  })
})

describe('getStateConfig loginMode', () => {
  it('uses email one-time-code login for DC', () => {
    expect(getStateConfig('dc').loginMode).toBe('otp')
  })

  it('uses OIDC login for CO', () => {
    expect(getStateConfig('co').loginMode).toBe('oidc')
  })

  it('falls back to the default config, and so to otp, for an unknown state code', () => {
    expect(getStateConfig('xx' as StateCode).loginMode).toBe('otp')
  })
})
