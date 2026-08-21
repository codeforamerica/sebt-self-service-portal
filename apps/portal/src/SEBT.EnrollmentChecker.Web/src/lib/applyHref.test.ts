import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

const CONFIGURED_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'

beforeEach(() => {
  // Neutralize any ambient NEXT_PUBLIC_APPLICATION_URL (CI sets one) so the
  // unset cases below are deterministic. Config tests override per-test.
  vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', '')
})

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('getApplyHref', () => {
  describe('no configured URL (graceful degradation)', () => {
    // With no application destination configured there is nothing to link to;
    // callers hide their apply link blocks on null instead of rendering a dead
    // or fabricated URL.
    it('returns null for every locale', () => {
      for (const locale of ['en', 'es', 'fr', '']) {
        expect(getApplyHref(locale)).toBeNull()
      }
    })
  })

  describe('language param (configured URL)', () => {
    beforeEach(() => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', CONFIGURED_URL)
    })

    it('uses en_US for the en locale', () => {
      expect(getApplyHref('en')).toBe(`${CONFIGURED_URL}?language=en_US&redirectFromEC=Y`)
    })

    it('uses es for the es locale', () => {
      expect(getApplyHref('es')).toBe(`${CONFIGURED_URL}?language=es&redirectFromEC=Y`)
    })

    it('falls back to en_US for an unknown locale', () => {
      expect(getApplyHref('fr')).toBe(`${CONFIGURED_URL}?language=en_US&redirectFromEC=Y`)
    })
  })

  describe('redirectFromEC tracking param', () => {
    // CO CBMS / Deloitte read this flag on the PEAK referrer to count clicks that
    // originate in the Enrollment Checker, so it must be present on every apply link.
    it('is always present, regardless of language', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', CONFIGURED_URL)
      for (const locale of ['en', 'es', 'fr', '']) {
        expect(getApplyHref(locale)).toContain('redirectFromEC=Y')
      }
    })
  })

  describe('NEXT_PUBLIC_APPLICATION_URL config', () => {
    it('builds the link from the configured URL', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', 'https://apply.preprod.example.gov/start')
      expect(getApplyHref('en')).toBe(
        'https://apply.preprod.example.gov/start?language=en_US&redirectFromEC=Y'
      )
    })

    it('overwrites a language already baked into the configured URL', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', `${CONFIGURED_URL}?language=en_US`)
      expect(getApplyHref('es')).toBe(`${CONFIGURED_URL}?language=es&redirectFromEC=Y`)
    })

    it('preserves unrelated query params already on the configured URL', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', 'https://apply.example.gov/start?src=partner')
      const href = getApplyHref('en')
      expect(href).toContain('src=partner')
      expect(href).toContain('language=en_US')
      expect(href).toContain('redirectFromEC=Y')
    })
  })
})
