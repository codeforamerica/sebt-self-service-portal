import { describe, expect, it } from 'vitest'

import { getStateConfig } from './state'

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
