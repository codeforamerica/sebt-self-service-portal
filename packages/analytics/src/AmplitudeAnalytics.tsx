'use client'

import * as amplitude from '@amplitude/analytics-browser'
import { useEffect } from 'react'
import { initAmplitudeBridge } from './amplitude-bridge'

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
