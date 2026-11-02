import { afterEach, describe, expect, it, vi } from 'vitest'

import { getClientConfig } from './client-config'

afterEach(() => {
  delete window.__CHECKER_CONFIG__
  vi.restoreAllMocks()
})

describe('getClientConfig', () => {
  it('falls back to build-time env when no config.js is deployed', () => {
    // test-setup.ts seeds NEXT_PUBLIC_PORTAL_URL; local dev relies on this path.
    expect(getClientConfig().portalUrl).toBe('https://portal.example.gov')
  })

  // The whole point: the deployed bucket's config.js must win over whatever the
  // build happened to bake in, or one artifact cannot be promoted between
  // environments.
  it('prefers deployed config over the value baked in at build time', () => {
    window.__CHECKER_CONFIG__ = { portalUrl: 'https://portal.production.gov' }

    expect(getClientConfig().portalUrl).toBe('https://portal.production.gov')
  })

  it('enables a vendor that was absent at build time', () => {
    expect(getClientConfig().amplitudeApiKey).toBeUndefined()

    window.__CHECKER_CONFIG__ = { amplitudeApiKey: 'amp-runtime' }

    expect(getClientConfig().amplitudeApiKey).toBe('amp-runtime')
  })

  it('treats blank and whitespace-only overrides as not configured', () => {
    window.__CHECKER_CONFIG__ = { amplitudeApiKey: '   ', applicationUrl: '' }

    const config = getClientConfig()

    expect(config.amplitudeApiKey).toBeUndefined()
    expect(config.applicationUrl).toBeUndefined()
  })

  // config.js is written after the build, so env.ts never validates it. A bad URL
  // must degrade to "not configured" rather than reach a `new URL()` and throw.
  it('treats a malformed URL override as not configured', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    window.__CHECKER_CONFIG__ = {
      applicationUrl: 'not a url',
      portalUrl: 'https://portal.production.gov'
    }

    const config = getClientConfig()

    expect(config.applicationUrl).toBeUndefined()
    expect(config.portalUrl).toBe('https://portal.production.gov')
    expect(consoleError).toHaveBeenCalledOnce()
  })

  it('accepts booleans as real booleans or as their string forms', () => {
    window.__CHECKER_CONFIG__ = { checkerEnabled: false, showSchoolField: 'true' }

    const config = getClientConfig()

    expect(config.checkerEnabled).toBe(false)
    expect(config.showSchoolField).toBe(true)
  })

  describe('applicationUrlPeakParams', () => {
    afterEach(() => {
      vi.unstubAllEnvs()
    })

    it('defaults to off when neither config.js nor the build supplies it', () => {
      expect(getClientConfig().applicationUrlPeakParams).toBe(false)
    })

    it.each([
      [true, true],
      [false, false],
      ['true', true],
      ['false', false]
    ])('reads the %j override as %s', (override, expected) => {
      window.__CHECKER_CONFIG__ = { applicationUrlPeakParams: override }

      expect(getClientConfig().applicationUrlPeakParams).toBe(expected)
    })

    it('ignores an unrecognized override and keeps the default', () => {
      window.__CHECKER_CONFIG__ = { applicationUrlPeakParams: 'yes' }

      expect(getClientConfig().applicationUrlPeakParams).toBe(false)
    })

    it('lets a deployed false win over a build-time true', async () => {
      vi.stubEnv('NEXT_PUBLIC_APPLICATION_URL_PEAK_PARAMS', 'true')
      vi.resetModules()
      const { getClientConfig: freshGetClientConfig } = await import('./client-config')

      expect(freshGetClientConfig().applicationUrlPeakParams).toBe(true)

      window.__CHECKER_CONFIG__ = { applicationUrlPeakParams: false }
      expect(freshGetClientConfig().applicationUrlPeakParams).toBe(false)
    })
  })

  it('ignores a non-string override rather than coercing it', () => {
    window.__CHECKER_CONFIG__ = { portalUrl: 42 }

    expect(getClientConfig().portalUrl).toBe('https://portal.example.gov')
  })

  it('reads window per call so a later deploy value is observed', () => {
    window.__CHECKER_CONFIG__ = { siteImproveId: 'si-first' }
    expect(getClientConfig().siteImproveId).toBe('si-first')

    window.__CHECKER_CONFIG__ = { siteImproveId: 'si-second' }
    expect(getClientConfig().siteImproveId).toBe('si-second')
  })
})
