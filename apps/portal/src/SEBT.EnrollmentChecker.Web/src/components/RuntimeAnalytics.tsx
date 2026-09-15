'use client'

import { getClientConfig } from '@/lib/client-config'
import {
  AmplitudeAnalytics,
  MetaPixelAnalytics,
  MixpanelAnalytics,
  SiteImproveAnalytics
} from '@sebt/analytics'

/**
 * Renders the analytics and pixel tags from runtime config.
 *
 * These have to be a client component: the checker is a static export, so
 * anything a Server Component renders is frozen into the HTML at build time —
 * which is exactly what stopped keys set on a deployed environment from taking
 * effect. Reading `window.__CHECKER_CONFIG__` on the client instead means the
 * deployed `config.js` decides which vendors load.
 */
export function RuntimeAnalytics() {
  const { metaPixel, mixpanelToken, amplitudeApiKey, siteImproveId } = getClientConfig()

  return (
    <>
      {metaPixel && <MetaPixelAnalytics pixelId={metaPixel} />}
      {mixpanelToken && <MixpanelAnalytics token={mixpanelToken} />}
      {amplitudeApiKey && <AmplitudeAnalytics apiKey={amplitudeApiKey} />}
      {siteImproveId && <SiteImproveAnalytics siteId={siteImproveId} />}
    </>
  )
}
