import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getRuntimeConfig } from './runtime-config'

describe('getRuntimeConfig', () => {
  beforeEach(() => {
    vi.unstubAllEnvs()
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('reads unprefixed values from the process environment', () => {
    vi.stubEnv('GA_ID', 'G-ABC123')
    vi.stubEnv('AMPLITUDE_API_KEY', 'amp-key')
    vi.stubEnv('MIXPANEL_TOKEN', 'mix-token')
    vi.stubEnv('SITEIMPROVE_ID', 'si-id')
    vi.stubEnv('SOCURE_DI_SDK_KEY', 'socure-di-key')
    vi.stubEnv('SMARTY_EMBEDDED_KEY', 'smarty-key')

    expect(getRuntimeConfig()).toMatchObject({
      gaId: 'G-ABC123',
      amplitudeApiKey: 'amp-key',
      mixpanelToken: 'mix-token',
      siteImproveId: 'si-id',
      socureDiSdkKey: 'socure-di-key',
      smartyEmbeddedKey: 'smarty-key'
    })
  })

  // The whole point of the change: a value set after the bundle was built must
  // still reach the browser, so a later read has to observe the newer value.
  it('observes values changed after module load', () => {
    vi.stubEnv('GA_ID', 'G-FIRST')
    expect(getRuntimeConfig().gaId).toBe('G-FIRST')

    vi.stubEnv('GA_ID', 'G-SECOND')
    expect(getRuntimeConfig().gaId).toBe('G-SECOND')
  })

  it('treats blank and whitespace-only values as absent', () => {
    vi.stubEnv('GA_ID', '')
    vi.stubEnv('SMARTY_EMBEDDED_KEY', '   ')

    const config = getRuntimeConfig()

    expect(config.gaId).toBeUndefined()
    expect(config.smartyEmbeddedKey).toBeUndefined()
  })

  it('trims surrounding whitespace so a padded value still enables the vendor', () => {
    vi.stubEnv('AMPLITUDE_API_KEY', '  amp-key  ')

    expect(getRuntimeConfig().amplitudeApiKey).toBe('amp-key')
  })

  it('exposes the development toggle as a boolean, defaulting to false', () => {
    expect(getRuntimeConfig().debugRepeatOidcStepUp).toBe(false)

    vi.stubEnv('DEBUG_REPEAT_OIDC_STEP_UP', 'true')

    expect(getRuntimeConfig().debugRepeatOidcStepUp).toBe(true)
  })

  it('treats any non-"true" toggle value as off', () => {
    vi.stubEnv('DEBUG_REPEAT_OIDC_STEP_UP', 'yes')

    expect(getRuntimeConfig().debugRepeatOidcStepUp).toBe(false)
  })
})
