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
    vi.stubEnv('APPLICATION_URL', 'https://example.gov/apply')

    expect(getRuntimeConfig()).toMatchObject({
      gaId: 'G-ABC123',
      amplitudeApiKey: 'amp-key',
      mixpanelToken: 'mix-token',
      siteImproveId: 'si-id',
      socureDiSdkKey: 'socure-di-key',
      smartyEmbeddedKey: 'smarty-key',
      applicationUrl: 'https://example.gov/apply'
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
    vi.stubEnv('APPLICATION_URL', '')

    const config = getRuntimeConfig()

    expect(config.gaId).toBeUndefined()
    expect(config.smartyEmbeddedKey).toBeUndefined()
    expect(config.applicationUrl).toBeUndefined()
  })

  it('omits the application URL when it is unset', () => {
    expect(getRuntimeConfig().applicationUrl).toBeUndefined()
  })

  it('trims the application URL so a padded value still resolves', () => {
    vi.stubEnv('APPLICATION_URL', '  https://example.gov/apply  ')

    expect(getRuntimeConfig().applicationUrl).toBe('https://example.gov/apply')
  })

  it('drops a malformed application URL and logs it, so the browser never parses it', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {})
    vi.stubEnv('APPLICATION_URL', 'peak.my.site.com/apply')

    expect(getRuntimeConfig().applicationUrl).toBeUndefined()
    expect(error).toHaveBeenCalledOnce()
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

  it('exposes the PEAK params toggle as a boolean, defaulting to false', () => {
    expect(getRuntimeConfig().applicationUrlPeakParams).toBe(false)

    vi.stubEnv('APPLICATION_URL_PEAK_PARAMS', 'true')

    expect(getRuntimeConfig().applicationUrlPeakParams).toBe(true)
  })

  it('reads the PEAK params toggle case-insensitively, as env.ts validates it', () => {
    vi.stubEnv('APPLICATION_URL_PEAK_PARAMS', ' TRUE ')

    expect(getRuntimeConfig().applicationUrlPeakParams).toBe(true)
  })

  it('treats any non-"true" PEAK params value, including blank, as off', () => {
    vi.stubEnv('APPLICATION_URL_PEAK_PARAMS', 'yes')
    expect(getRuntimeConfig().applicationUrlPeakParams).toBe(false)

    vi.stubEnv('APPLICATION_URL_PEAK_PARAMS', '')
    expect(getRuntimeConfig().applicationUrlPeakParams).toBe(false)
  })
})
