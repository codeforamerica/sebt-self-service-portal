'use client'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { useEffect, useRef } from 'react'

import { useAuth } from '@/features/auth'
import { useHouseholdData } from '@/features/household'
import { syncColoadingStatus } from '@/lib/analytics-helpers'

type FlowStartEvent =
  | typeof AnalyticsEvents.ADDRESS_UPDATE_START
  | typeof AnalyticsEvents.CARD_REPLACEMENT_START

/**
 * Fires a flow-start analytics event once household data is available.
 * Defers to the next animation frame so page.name from PageTracker is set first.
 */
export function useFlowStartAnalytics(eventName: FlowStartEvent, enabled = true) {
  const { session } = useAuth()
  const { data } = useHouseholdData()
  const { setUserData, trackEvent } = useDataLayer()
  const tracked = useRef(false)

  useEffect(() => {
    if (!enabled || tracked.current || !data) return

    const raf = requestAnimationFrame(() => {
      if (tracked.current) return
      tracked.current = true
      syncColoadingStatus(setUserData, session?.isCoLoaded, data)
      trackEvent(eventName)
    })

    return () => cancelAnimationFrame(raf)
  }, [enabled, data, session?.isCoLoaded, setUserData, trackEvent, eventName])
}
