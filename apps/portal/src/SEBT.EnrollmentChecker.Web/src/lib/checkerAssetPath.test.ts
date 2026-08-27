import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getCheckerAssetPath } from './checkerAssetPath'

beforeEach(() => {
  // CI may export a base path; neutralize it so each case sets its own.
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
    // DC's landing page goes straight from toolbar to <h1>.
    it('resolves the landing logo for CO and omits it for DC', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
      expect(getCheckerAssetPath('landingLogo')).toBe('/images/states/co/summer-ebt-logo.svg')

      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('landingLogo')).toBeUndefined()
    })

    // DC has no alert artwork, so its error screen renders without a decorative
    // icon rather than borrowing the wrong one.
    it('resolves the error card for every state that ships one', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
      expect(getCheckerAssetPath('errorCard')).toBe('/images/states/co/icon-alert-card.svg')

      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('errorCard')).toBe('/images/states/dc/icon-alert-card.svg')
    })

    it('omits a slot the state has no artwork for', () => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('landingLogo')).toBeUndefined()
    })
  })

  describe('per-state results icons', () => {
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

      // Not enrolled is an invitation to apply, so DC keeps the review artwork
      // here and saves the alert artwork for the error screen.
      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
      expect(getCheckerAssetPath('resultsNotEnrolled')).toBe(
        '/images/states/dc/icon-review-card.svg'
      )
      expect(getCheckerAssetPath('errorCard')).toBe('/images/states/dc/icon-alert-card.svg')
    })
  })
})
