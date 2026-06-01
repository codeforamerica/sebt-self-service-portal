import { describe, expect, it } from 'vitest'

import { getStateConfig, getSiteDisplayName } from './state'

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

  it('returns DC SUN Bucks for DC', () => {
    expect(getSiteDisplayName('dc')).toBe('DC SUN Bucks')
  })
})
