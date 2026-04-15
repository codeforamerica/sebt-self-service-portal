'use client'

import { initMixpanelBridge } from '@sebt/analytics'
import Script from 'next/script'
import { useEffect, useRef } from 'react'

interface MixpanelAnalyticsProps {
  token: string
  nonce?: string
}

export function MixpanelAnalytics({ token, nonce }: MixpanelAnalyticsProps) {
  const teardownRef = useRef<(() => void) | null>(null)

  useEffect(() => {
    return () => {
      teardownRef.current?.()
      teardownRef.current = null
    }
  }, [token])

  const scriptProps = {
    src: 'https://cdn.mxpnl.com/libs/mixpanel-2-latest.min.js',
    strategy: 'afterInteractive' as const,
    nonce,
    onLoad: () => {
      if (teardownRef.current) return
      teardownRef.current = initMixpanelBridge(token)
    }
  }

  return <Script {...scriptProps} />
}
