'use client'

import Script from 'next/script'
import { useEffect, useRef } from 'react'
import { initSiteImproveBridge } from './siteimprove-bridge'

interface SiteImproveAnalyticsProps {
  siteId: string
  // Caller supplies the current state so this package stays free of the
  // design-system dependency. Callers should also gate rendering on state at
  // the layout level; this check is defense in depth.
  state: string
  nonce?: string
}

export function SiteImproveAnalytics({ siteId, state, nonce }: SiteImproveAnalyticsProps) {
  const teardownRef = useRef<(() => void) | null>(null)

  useEffect(() => {
    return () => {
      teardownRef.current?.()
      teardownRef.current = null
    }
  }, [siteId])

  // DC-only per DC-272. The layout-level state gate already requires state===dc;
  // this check is defense in depth so an accidentally-set env var in another
  // state still can't load SiteImprove.
  if (state !== 'dc') return null

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
