import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

const CONFIGURED_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'

/** The apply destination now comes from runtime client config, not an env var. */
function setApplicationUrl(applicationUrl: string) {
  window.__CHECKER_CONFIG__ = { applicationUrl }
}

beforeEach(() => {
  // Neutralize any ambient value (CI sets one) so the unset cases below are
  // deterministic. Config tests override per-test.
  vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', '')
  setApplicationUrl('')
})

afterEach(() => {
  vi.unstubAllEnvs()
  vi.restoreAllMocks()
  delete window.__CHECKER_CONFIG__
})

// The PEAK cases below run under CO, the state vitest pins.
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

  describe('malformed configured URL', () => {
    // config.js is written after the build, so a bad applicationUrl is never seen
    // by env.ts. It must hide the apply link rather than throw from `new URL()`.
    it('returns null instead of throwing', () => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      setApplicationUrl('not a url')

      expect(getApplyHref('en')).toBeNull()
    })
  })

  describe('language param (configured URL)', () => {
    beforeEach(() => {
      setApplicationUrl(CONFIGURED_URL)
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
      setApplicationUrl(CONFIGURED_URL)
      for (const locale of ['en', 'es', 'fr', '']) {
        expect(getApplyHref(locale)).toContain('redirectFromEC=Y')
      }
    })
  })

  describe('states without a PEAK destination', () => {
    const DC_URL = 'https://apply.dc.example.gov/start'

    beforeEach(() => {
      vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    })

    // language and redirectFromEC are PEAK's contract. Sending them to another
    // state's destination is at best noise and at worst a wrong destination.
    it('links to the configured URL untouched', () => {
      setApplicationUrl(DC_URL)
      const href = getApplyHref('en')
      expect(href).toBe(DC_URL)
      expect(href).not.toContain('language=')
      expect(href).not.toContain('redirectFromEC')
    })

    it('adds nothing to a URL that already carries params', () => {
      setApplicationUrl(`${DC_URL}?src=partner`)
      expect(getApplyHref('es')).toBe(`${DC_URL}?src=partner`)
    })

    it('still returns null when no URL is configured', () => {
      for (const locale of ['en', 'es', 'am', '']) {
        expect(getApplyHref(locale)).toBeNull()
      }
    })
  })

  describe('applicationUrl config', () => {
    it('builds the link from the configured URL', () => {
      setApplicationUrl('https://apply.preprod.example.gov/start')
      expect(getApplyHref('en')).toBe(
        'https://apply.preprod.example.gov/start?language=en_US&redirectFromEC=Y'
      )
    })

    it('overwrites a language already baked into the configured URL', () => {
      setApplicationUrl(`${CONFIGURED_URL}?language=en_US`)
      expect(getApplyHref('es')).toBe(`${CONFIGURED_URL}?language=es&redirectFromEC=Y`)
    })

    it('preserves unrelated query params already on the configured URL', () => {
      setApplicationUrl('https://apply.example.gov/start?src=partner')
      const href = getApplyHref('en')
      expect(href).toContain('src=partner')
      expect(href).toContain('language=en_US')
      expect(href).toContain('redirectFromEC=Y')
    })
  })
})
