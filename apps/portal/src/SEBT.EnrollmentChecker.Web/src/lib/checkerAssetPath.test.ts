import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getCheckerAssetPath } from './checkerAssetPath'

beforeEach(() => {
  // vitest.config.ts pins NEXT_PUBLIC_STATE=co process-wide and CI may export a
  // base path; stub both so each case states its own starting conditions.
  vi.stubEnv('NEXT_PUBLIC_BASE_PATH', '')
})

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('getCheckerAssetPath', () => {
  describe('state interpolation', () => {
    it('resolves against the active state', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
      expect(getCheckerAssetPath('formCard')).toBe('/images/states/co/icon-form-card.svg')

      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('formCard')).toBe('/images/states/dc/icon-form-card.svg')
    })

    it('falls back to the design system default state when unset', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', '')
      expect(getCheckerAssetPath('formCard')).toBe('/images/states/dc/icon-form-card.svg')
    })

    it('returns undefined for a state with no asset configuration', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'zz')
      expect(getCheckerAssetPath('formCard')).toBeUndefined()
    })
  })

  describe('base path', () => {
    beforeEach(() => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
    })

    it('returns a root-relative path when no base path is configured', () => {
      expect(getCheckerAssetPath('formCard')).toBe('/images/states/co/icon-form-card.svg')
    })

    it('prefixes a configured base path', () => {
      vi.stubEnv('NEXT_PUBLIC_BASE_PATH', '/checker')
      expect(getCheckerAssetPath('formCard')).toBe('/checker/images/states/co/icon-form-card.svg')
    })

    it('does not double the separator when the base path has a trailing slash', () => {
      vi.stubEnv('NEXT_PUBLIC_BASE_PATH', '/checker/')
      expect(getCheckerAssetPath('formCard')).toBe('/checker/images/states/co/icon-form-card.svg')
    })
  })

  describe('optional slots', () => {
    // DC's landing page goes straight from toolbar to <h1> — its branding lives
    // in the toolbar logo, so there is no landing lockup to render.
    it('resolves the landing logo for CO and omits it for DC', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
      expect(getCheckerAssetPath('landingLogo')).toBe('/images/states/co/summer-ebt-logo.svg')

      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('landingLogo')).toBeUndefined()
    })

    // DC has no alert artwork yet (DC-727 open question); the error screen
    // renders without a decorative icon rather than borrowing the wrong one.
    it('resolves the error card for CO and omits it for DC', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
      expect(getCheckerAssetPath('errorCard')).toBe('/images/states/co/icon-alert-card.svg')

      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('errorCard')).toBeUndefined()
    })
  })

  describe('per-state results icons', () => {
    // The outcome-to-artwork mapping is genuinely state-specific: CO leads an
    // enrolled result with the review card, DC with a checkmark.
    it('maps the enrolled outcome per state', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
      expect(getCheckerAssetPath('resultsEnrolled')).toBe('/images/states/co/icon-review-card.svg')

      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('resultsEnrolled')).toBe(
        '/images/states/dc/icon-checkmark-card.svg'
      )
    })

    it('maps the not-enrolled outcome per state', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
      expect(getCheckerAssetPath('resultsNotEnrolled')).toBe(
        '/images/states/co/icon-alert-card.svg'
      )

      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('resultsNotEnrolled')).toBe(
        '/images/states/dc/icon-review-card.svg'
      )
    })
  })
})
