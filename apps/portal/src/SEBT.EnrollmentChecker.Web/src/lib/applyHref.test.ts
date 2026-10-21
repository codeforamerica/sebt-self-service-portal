import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

const PEAK_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'
const OTHER_URL = 'https://apply.dc.example.gov/start'

/** The apply destination and its PEAK switch both come from runtime client config. */
function setConfig(applicationUrl: string, applicationUrlPeakParams = false) {
  window.__CHECKER_CONFIG__ = { applicationUrl, applicationUrlPeakParams }
}

/** The state must not matter, so cases pin it explicitly rather than rely on vitest's default. */
function setState(state: 'co' | 'dc') {
  vi.stubEnv('STATE', state)
  vi.stubEnv('NEXT_PUBLIC_STATE', state)
}

beforeEach(() => {
  // Neutralize any ambient value (CI sets one) so the unset cases below are
  // deterministic. Config tests override per-test.
  vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', '')
  setConfig('')
})

afterEach(() => {
  vi.unstubAllEnvs()
  vi.restoreAllMocks()
  delete window.__CHECKER_CONFIG__
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

    it('returns null even when PEAK params are on', () => {
      setConfig('', true)
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
      setConfig('not a url', true)

      expect(getApplyHref('en')).toBeNull()
    })
  })

  describe('PEAK params on', () => {
    beforeEach(() => {
      setConfig(PEAK_URL, true)
    })

    it('uses en_US for the en locale', () => {
      expect(getApplyHref('en')).toBe(`${PEAK_URL}?language=en_US&redirectFromEC=Y`)
    })

    it('uses es for the es locale', () => {
      expect(getApplyHref('es')).toBe(`${PEAK_URL}?language=es&redirectFromEC=Y`)
    })

    it('falls back to en_US for an unknown locale', () => {
      expect(getApplyHref('fr')).toBe(`${PEAK_URL}?language=en_US&redirectFromEC=Y`)
      expect(getApplyHref('')).toBe(`${PEAK_URL}?language=en_US&redirectFromEC=Y`)
    })

    // CO CBMS / Deloitte read this flag on the PEAK referrer to count clicks that
    // originate in the Enrollment Checker, so it must be present on every apply link.
    it('always adds redirectFromEC, regardless of language', () => {
      for (const locale of ['en', 'es', 'fr', '']) {
        expect(getApplyHref(locale)).toContain('redirectFromEC=Y')
      }
    })

    it('overwrites a language already baked into the configured URL', () => {
      setConfig(`${PEAK_URL}?language=en_US`, true)
      expect(getApplyHref('es')).toBe(`${PEAK_URL}?language=es&redirectFromEC=Y`)
    })

    it('preserves unrelated query params already on the configured URL', () => {
      setConfig(`${OTHER_URL}?src=partner`, true)
      const href = getApplyHref('en')
      expect(href).toContain('src=partner')
      expect(href).toContain('language=en_US')
      expect(href).toContain('redirectFromEC=Y')
    })

    it('adds the params under dc too, since the state does not decide', () => {
      setState('dc')
      expect(getApplyHref('es')).toBe(`${PEAK_URL}?language=es&redirectFromEC=Y`)
    })
  })

  describe('PEAK params off', () => {
    // language and redirectFromEC are PEAK's contract. Sending them to another
    // destination is at best noise and at worst a wrong destination.
    it('links to the configured URL untouched', () => {
      setConfig(OTHER_URL)
      const href = getApplyHref('en')
      expect(href).toBe(OTHER_URL)
      expect(href).not.toContain('language=')
      expect(href).not.toContain('redirectFromEC')
    })

    it('adds nothing to a URL that already carries params', () => {
      setConfig(`${OTHER_URL}?src=partner`)
      expect(getApplyHref('es')).toBe(`${OTHER_URL}?src=partner`)
    })

    it('leaves a language already on the URL alone', () => {
      setConfig(`${PEAK_URL}?language=en_US`)
      expect(getApplyHref('es')).toBe(`${PEAK_URL}?language=en_US`)
    })

    it('leaves the URL untouched under co, since the state does not decide', () => {
      setState('co')
      setConfig(PEAK_URL)
      expect(getApplyHref('en')).toBe(PEAK_URL)
    })

    it('leaves the URL untouched under dc', () => {
      setState('dc')
      setConfig(OTHER_URL)
      expect(getApplyHref('en')).toBe(OTHER_URL)
    })

    it('is the default when config.js does not set the switch', () => {
      window.__CHECKER_CONFIG__ = { applicationUrl: PEAK_URL }
      expect(getApplyHref('en')).toBe(PEAK_URL)
    })
  })

  describe('applicationUrl config', () => {
    it('builds the link from the configured URL', () => {
      setConfig('https://apply.preprod.example.gov/start', true)
      expect(getApplyHref('en')).toBe(
        'https://apply.preprod.example.gov/start?language=en_US&redirectFromEC=Y'
      )
    })
  })
})
