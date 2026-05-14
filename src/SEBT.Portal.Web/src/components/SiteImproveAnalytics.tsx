'use client'

import { initSiteImproveBridge } from '@sebt/analytics'
import { getState } from '@sebt/design-system'
import Script from 'next/script'
import { useEffect, useRef } from 'react'

interface SiteImproveAnalyticsProps {
  siteId: string
  nonce?: string
}

export function SiteImproveAnalytics({ siteId, nonce }: SiteImproveAnalyticsProps) {
  const teardownRef = useRef<(() => void) | null>(null)

  useEffect(() => {
    return () => {
      teardownRef.current?.()
      teardownRef.current = null
    }
  }, [siteId])

  // DC-only per DC-272. The layout-level env-var gate already requires
  // NEXT_PUBLIC_SITEIMPROVE_ID; this second check is defense in depth so an
  // accidentally-set env var in another state still can't load SiteImprove.
  if (getState() !== 'dc') return null

  return (
    <Script
      id="siteimprove-analytics"
      src={`https://siteimproveanalytics.com/js/siteanalyze_${encodeURIComponent(siteId)}.js`}
      strategy="afterInteractive"
      nonce={nonce}
      onLoad={() => {
        if (teardownRef.current) return
        teardownRef.current = initSiteImproveBridge()
      }}
    />
  )
}
