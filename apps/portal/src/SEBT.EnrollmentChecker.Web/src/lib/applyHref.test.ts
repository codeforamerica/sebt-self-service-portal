import { afterEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

// The PEAK starting page the helper falls back to when NEXT_PUBLIC_APPLICATION_URL
// is unset (local/test runs).
const DEFAULT_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('getApplyHref', () => {
  describe('language param', () => {
    it('uses en_US for the en locale', () => {
      expect(getApplyHref('en')).toBe(`${DEFAULT_URL}?language=en_US&redirectFromEC=Y`)
    })

    it('uses es for the es locale', () => {
      expect(getApplyHref('es')).toBe(`${DEFAULT_URL}?language=es&redirectFromEC=Y`)
    })

    it('falls back to en_US for an unknown locale', () => {
      expect(getApplyHref('fr')).toBe(`${DEFAULT_URL}?language=en_US&redirectFromEC=Y`)
    })
  })

  describe('redirectFromEC tracking param', () => {
    // CO CBMS / Deloitte read this flag on the PEAK referrer to count clicks that
    // originate in the Enrollment Checker, so it must be present on every apply link.
    it('is always present, regardless of language', () => {
      for (const locale of ['en', 'es', 'fr', '']) {
        expect(getApplyHref(locale)).toContain('redirectFromEC=Y')
      }
    })
  })

  describe('NEXT_PUBLIC_APPLICATION_URL config', () => {
    it('builds the link from the configured URL instead of the default', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', 'https://apply.preprod.example.gov/start')
      expect(getApplyHref('en')).toBe(
        'https://apply.preprod.example.gov/start?language=en_US&redirectFromEC=Y'
      )
    })

    it('overwrites a language already baked into the configured URL', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', `${DEFAULT_URL}?language=en_US`)
      expect(getApplyHref('es')).toBe(`${DEFAULT_URL}?language=es&redirectFromEC=Y`)
    })

    it('preserves unrelated query params already on the configured URL', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', 'https://apply.example.gov/start?src=partner')
      const href = getApplyHref('en')
      expect(href).toContain('src=partner')
      expect(href).toContain('language=en_US')
      expect(href).toContain('redirectFromEC=Y')
    })

    it('falls back to the PEAK default when the var is empty', () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL', '')
      expect(getApplyHref('en')).toBe(`${DEFAULT_URL}?language=en_US&redirectFromEC=Y`)
    })
  })
})
