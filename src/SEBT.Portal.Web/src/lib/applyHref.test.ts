import { afterEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return { ...actual, getState: () => mockState }
})

afterEach(() => {
  mockState = 'dc'
})

describe('getApplyHref', () => {
  it('returns the CO PEAK starting-page URL with English language param when locale is en', () => {
    mockState = 'co'
    expect(getApplyHref('en')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })

  it('returns the CO PEAK starting-page URL with Spanish language param when locale is es', () => {
    mockState = 'co'
    expect(getApplyHref('es')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=es_US'
    )
  })

  it('falls back to en_US for an unknown locale on CO', () => {
    mockState = 'co'
    expect(getApplyHref('fr')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })

  it('returns the DC application form URL when state is dc, regardless of locale', () => {
    mockState = 'dc'
    expect(getApplyHref('en')).toBe('https://forms.sunbucks.dc.gov/s3/AppUpdate2026')
    expect(getApplyHref('es')).toBe('https://forms.sunbucks.dc.gov/s3/AppUpdate2026')
  })

  it('falls back to /apply for an unknown state', () => {
    mockState = 'xx'
    expect(getApplyHref('en')).toBe('/apply')
  })
})
