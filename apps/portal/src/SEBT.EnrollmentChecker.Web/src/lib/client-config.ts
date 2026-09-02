import { env } from './env'

/**
 * Browser-facing configuration for the enrollment checker.
 *
 * The checker ships as a static export (`output: 'export'`) synced to S3, so
 * there is no server to read `process.env` at request time — the portal's
 * approach doesn't transfer. Instead the deployed bucket carries a `config.js`
 * that assigns `window.__CHECKER_CONFIG__`, loaded from `<head>` before the app
 * bundle. That keeps config out of the build artifact while still making it
 * available synchronously, so analytics and pixels initialize on first paint
 * rather than after a fetch settles.
 *
 * Values fall back to the build-time `env` so local development and tests keep
 * working from `.env` without a `config.js` present.
 *
 * `NEXT_PUBLIC_STATE` and `NEXT_PUBLIC_BASE_PATH` are deliberately absent: both
 * select build-time output (per-state assets, and Next's `basePath`, which
 * rewrites every emitted asset URL), so neither can move to runtime.
 */
export interface CheckerClientConfig {
  apiBaseUrl?: string | undefined
  /** Absent when neither config.js nor the build-time fallback supplies one. */
  portalUrl?: string | undefined
  applicationUrl?: string | undefined
  showSchoolField: boolean
  checkerEnabled: boolean
  botProtectionEnabled: boolean
  amplitudeApiKey?: string | undefined
  mixpanelToken?: string | undefined
  siteImproveId?: string | undefined
  metaPixel?: string | undefined
  metaPixelAction?: string | undefined
  adentifiPixelLanding?: string | undefined
  adentifiPixelApplyNow?: string | undefined
}

/** Shape written by the deployed `config.js`; every key is optional. */
type ConfigOverrides = Partial<Record<keyof CheckerClientConfig, unknown>>

declare global {
  interface Window {
    __CHECKER_CONFIG__?: ConfigOverrides
  }
}

function overrides(): ConfigOverrides {
  return typeof window === 'undefined' ? {} : (window.__CHECKER_CONFIG__ ?? {})
}

/** Blank and whitespace-only values mean "not configured", matching emptyStringAsUndefined. */
function str(override: unknown, fallback: string | undefined): string | undefined {
  const value = typeof override === 'string' ? override : fallback
  const trimmed = value?.trim()
  return trimmed ? trimmed : undefined
}

function bool(override: unknown, fallback: boolean): boolean {
  if (typeof override === 'boolean') return override
  if (override === 'true') return true
  if (override === 'false') return false
  return fallback
}

/**
 * Resolves browser config, preferring the deployed `config.js` over the values
 * baked in at build time. Read per call rather than cached at module scope so a
 * test can swap `window.__CHECKER_CONFIG__` between cases.
 */
export function getClientConfig(): CheckerClientConfig {
  const o = overrides()
  return {
    apiBaseUrl: str(o.apiBaseUrl, env.NEXT_PUBLIC_API_BASE_URL),
    portalUrl: str(o.portalUrl, env.NEXT_PUBLIC_PORTAL_URL),
    applicationUrl: str(o.applicationUrl, env.NEXT_PUBLIC_APPLICATION_URL),
    showSchoolField: bool(o.showSchoolField, env.NEXT_PUBLIC_SHOW_SCHOOL_FIELD),
    checkerEnabled: bool(o.checkerEnabled, env.NEXT_PUBLIC_CHECKER_ENABLED),
    botProtectionEnabled: bool(o.botProtectionEnabled, env.NEXT_PUBLIC_BOT_PROTECTION_ENABLED),
    amplitudeApiKey: str(o.amplitudeApiKey, env.NEXT_PUBLIC_AMPLITUDE_API_KEY),
    mixpanelToken: str(o.mixpanelToken, env.NEXT_PUBLIC_MIXPANEL_TOKEN),
    siteImproveId: str(o.siteImproveId, env.NEXT_PUBLIC_SITEIMPROVE_ID),
    metaPixel: str(o.metaPixel, env.NEXT_PUBLIC_META_PIXEL),
    metaPixelAction: str(o.metaPixelAction, env.NEXT_PUBLIC_META_PIXEL_ACTION),
    adentifiPixelLanding: str(o.adentifiPixelLanding, env.NEXT_PUBLIC_ADENTIFI_PIXEL_LANDING),
    adentifiPixelApplyNow: str(o.adentifiPixelApplyNow, env.NEXT_PUBLIC_ADENTIFI_PIXEL_APPLY_NOW)
  }
}
