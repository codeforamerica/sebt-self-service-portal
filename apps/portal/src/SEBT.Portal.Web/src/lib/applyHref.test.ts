import { afterEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

const PEAK_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return { ...actual, getState: () => mockState }
})

afterEach(() => {
  mockState = 'dc'
})

describe('getApplyHref', () => {
  describe('without an application URL', () => {
    it.each([true, false])('returns null when peakParams is %s', (peakParams) => {
      expect(getApplyHref('en', { peakParams })).toBeNull()
      expect(getApplyHref('en', { applicationUrl: undefined, peakParams })).toBeNull()
    })
  })

  describe('with peakParams off', () => {
    it.each(['dc', 'co', 'xx'])('returns the URL unchanged in state %s', (state) => {
      mockState = state
      expect(getApplyHref('en', { applicationUrl: PEAK_URL, peakParams: false })).toBe(PEAK_URL)
    })

    it('adds no language param for any locale', () => {
      expect(
        getApplyHref('es', { applicationUrl: 'https://example.gov/apply', peakParams: false })
      ).toBe('https://example.gov/apply')
    })

    it('keeps an existing query string as given', () => {
      const url = 'https://example.gov/apply?source=portal'
      expect(getApplyHref('en', { applicationUrl: url, peakParams: false })).toBe(url)
    })
  })

  describe('with peakParams on', () => {
    const config = { applicationUrl: PEAK_URL, peakParams: true }

    it('adds the English language param when locale is en', () => {
      expect(getApplyHref('en', config)).toBe(`${PEAK_URL}?language=en_US`)
    })

    it('adds the Spanish language param when locale is es', () => {
      expect(getApplyHref('es', config)).toBe(`${PEAK_URL}?language=es_US`)
    })

    it('falls back to en_US for an unknown locale', () => {
      expect(getApplyHref('fr', config)).toBe(`${PEAK_URL}?language=en_US`)
    })

    it('overwrites a language param already on the URL', () => {
      expect(
        getApplyHref('es', { applicationUrl: `${PEAK_URL}?language=en_US`, peakParams: true })
      ).toBe(`${PEAK_URL}?language=es_US`)
    })

    it('keeps other query params on the URL', () => {
      expect(
        getApplyHref('en', { applicationUrl: `${PEAK_URL}?source=portal`, peakParams: true })
      ).toBe(`${PEAK_URL}?source=portal&language=en_US`)
    })
  })
})
