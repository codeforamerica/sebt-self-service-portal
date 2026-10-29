import type { RuntimeConfig } from '@/lib/runtime-config'

/** An otherwise unconfigured RuntimeConfig; pass overrides for the values a test cares about. */
export function makeRuntimeConfig(overrides: Partial<RuntimeConfig> = {}): RuntimeConfig {
  return { debugRepeatOidcStepUp: false, applicationUrlPeakParams: false, ...overrides }
}

export const PEAK_APPLY_URL = 'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page'

/** Colorado's apply destination: PEAK's form, which takes a `language` param. */
export const PEAK_APPLY_CONFIG = makeRuntimeConfig({
  applicationUrl: PEAK_APPLY_URL,
  applicationUrlPeakParams: true
})
