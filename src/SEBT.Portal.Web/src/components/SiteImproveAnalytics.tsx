'use client'

import { initSiteImproveBridge } from '@sebt/analytics'
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
