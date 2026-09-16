import { afterEach, describe, expect, it, vi } from 'vitest'

import { getLandingActions, getLandingConfig } from './landingConfig'

afterEach(() => {
  vi.unstubAllEnvs()
})

describe('getLandingConfig', () => {
  // DC shows the same copy inline and has no accordionTitle to label a control.
  it('enables the accordion for CO and disables it for DC', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
    expect(getLandingConfig().useAccordion).toBe(true)

    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getLandingConfig().useAccordion).toBe(false)
  })
})

describe('getLandingActions', () => {
  it('offers one start button per language the state supports', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'co')
    expect(getLandingActions().map((a) => a.language)).toEqual(['en', 'es'])

    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getLandingActions().map((a) => a.language)).toEqual(['en', 'es', 'am'])
  })

  // Content keys aren't uniform, so the mapping is explicit.
  it('maps each language to its content key', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getLandingActions().map((a) => a.translationKey)).toEqual([
      'action',
      'actionEspañol',
      'actionAmharic'
    ])
  })

  // The post-season page reframes the same action, so it needs its own labels.
  it('maps each language to its closed-variant content key', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getLandingActions('closed').map((a) => a.translationKey)).toEqual([
      'closedAction',
      'closedActionEspañol',
      'closedActionAmharic'
    ])
  })

  it('keeps analytics identifiers stable across variants', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getLandingActions('closed').map((a) => a.analyticsCta)).toEqual(
      getLandingActions('open').map((a) => a.analyticsCta)
    )
  })

  // These feed existing analytics dashboards and must not change.
  it('preserves the established analytics CTA identifiers', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    expect(getLandingActions().map((a) => a.analyticsCta)).toEqual([
      'start_enrollment_check_cta',
      'start_enrollment_check_cta_es',
      'start_enrollment_check_cta_am'
    ])
  })

  // Hierarchy shouldn't change as states add languages.
  it('renders the first language as the primary button and the rest as outline', () => {
    vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
    const variants = getLandingActions().map((a) => a.variant)
    expect(variants[0]).toBeUndefined()
    expect(variants.slice(1)).toEqual(['outline', 'outline'])
  })
})
