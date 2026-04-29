'use client'

import * as amplitude from '@amplitude/analytics-browser'
import { initAmplitudeBridge } from '@sebt/analytics'
import { useEffect } from 'react'

interface AmplitudeAnalyticsProps {
  apiKey: string
}

export function AmplitudeAnalytics({ apiKey }: AmplitudeAnalyticsProps) {
  useEffect(() => {
    const teardown = initAmplitudeBridge(apiKey, amplitude)
    return teardown
  }, [apiKey])

  return null
}
