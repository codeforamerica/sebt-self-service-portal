/**
 * Browser-facing configuration, read from the server process environment at
 * request time rather than inlined into the client bundle at build time.
 *
 * Why these are not `NEXT_PUBLIC_*`: Next inlines any `NEXT_PUBLIC_`-prefixed
 * reference into the static chunks during `next build`, so the value is fixed
 * by the build environment. Setting it later on the container or in IIS
 * `web.config` cannot change what the browser receives, which means one build
 * artifact cannot be promoted across environments. Dropping the prefix keeps
 * the values server-side, where the Next server reads them per request and
 * hands them to the client through `RuntimeConfigProvider`.
 *
 * Server-only: this reads `process.env` names that are deliberately absent from
 * the client bundle. Import it from server components (or route handlers), and
 * reach the values in client components via `useRuntimeConfig()`.
 */

/** Browser-facing configuration resolved at request time. */
export interface RuntimeConfig {
  /** Google Analytics measurement id (`G-…`). Omitted when analytics is off. */
  gaId?: string | undefined
  /** Mixpanel project token. Omitted when Mixpanel is off. */
  mixpanelToken?: string | undefined
  /** Amplitude API key. Omitted when Amplitude is off. */
  amplitudeApiKey?: string | undefined
  /** SiteImprove analytics id. Omitted when SiteImprove is off. */
  siteImproveId?: string | undefined
  /** Socure Device Intelligence SDK key. Omitted when DI is off. */
  socureDiSdkKey?: string | undefined
  /** Smarty US Autocomplete Pro embeddable key. Omitted to disable type-ahead. */
  smartyEmbeddedKey?: string | undefined
  /** Swap the Socure document-verification adapter for the in-browser mock. */
  mockSocure: boolean
  /**
   * Development only: keep sending users through OIDC step-up even when the
   * portal JWT already carries IAL1+. Ignored outside development.
   */
  debugRepeatOidcStepUp: boolean
}

/** Treats blank and whitespace-only values as absent, matching `emptyStringAsUndefined` in env.ts. */
function optional(value: string | undefined): string | undefined {
  const trimmed = value?.trim()
  return trimmed ? trimmed : undefined
}

/**
 * Reads browser-facing configuration from the server process environment.
 *
 * Call this per request from a server component. Reading at module scope would
 * freeze the values for the lifetime of the process, which still beats build
 * time but defeats a config change applied by a restart-free release.
 */
export function getRuntimeConfig(): RuntimeConfig {
  return {
    gaId: optional(process.env.GA_ID),
    mixpanelToken: optional(process.env.MIXPANEL_TOKEN),
    amplitudeApiKey: optional(process.env.AMPLITUDE_API_KEY),
    siteImproveId: optional(process.env.SITEIMPROVE_ID),
    socureDiSdkKey: optional(process.env.SOCURE_DI_SDK_KEY),
    smartyEmbeddedKey: optional(process.env.SMARTY_EMBEDDED_KEY),
    mockSocure: process.env.MOCK_SOCURE === 'true',
    debugRepeatOidcStepUp: process.env.DEBUG_REPEAT_OIDC_STEP_UP === 'true'
  }
}
