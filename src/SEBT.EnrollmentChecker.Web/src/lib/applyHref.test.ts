import { afterEach, describe, expect, it, vi } from 'vitest'

import { getApplyHref } from './applyHref'

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('getApplyHref', () => {
  it('returns the PEAK starting-page URL with English language param when locale is en', () => {
    expect(getApplyHref('en')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })

  it('returns the PEAK starting-page URL with Spanish language param when locale is es', () => {
    expect(getApplyHref('es')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=es'
    )
  })

  it('falls back to en_US for an unknown locale', () => {
    expect(getApplyHref('fr')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })

  it('returns the PEAK URL when NEXT_PUBLIC_STATE is empty', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', '')
    expect(getApplyHref('en')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })

  it('returns the PEAK URL when NEXT_PUBLIC_STATE is dc', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getApplyHref('en')).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })
})
