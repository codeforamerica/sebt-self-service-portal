'use client'

import { useEffect, useRef } from 'react'

import { throttle } from './throttle'

/**
 * DOM events that count as user activity for idle-timeout purposes.
 *
 * Mirrors the OWASP Session Management guidance: any meaningful interaction
 * should reset the idle clock. Tab visibility transitions are included so a
 * user returning to the tab from another window doesn't get logged out
 * mid-action by a refresh that fires in the gap.
 */
const ACTIVITY_EVENTS = [
  'mousedown',
  'keydown',
  'touchstart',
  'scroll',
  'visibilitychange'
] as const

const ACTIVITY_THROTTLE_MS = 1000

export interface UserActivityTracker {
  /** Returns the timestamp (ms since epoch) of the most recent recorded activity. */
  getLastActivityAt: () => number
}

/**
 * Tracks the most recent user-activity timestamp via passive DOM listeners.
 * Listeners are throttled so a steady stream of events (e.g., scrolling) doesn't
 * thrash React state. The hook exposes a getter rather than re-rendering on
 * activity — consumers (the refresh scheduler) only sample on a timer.
 */
export function useUserActivity(): UserActivityTracker {
  // Initialized in the mount effect — keeps render pure.
  const lastActivityAtRef = useRef<number>(0)

  useEffect(() => {
    lastActivityAtRef.current = Date.now()

    const recordActivity = throttle(() => {
      lastActivityAtRef.current = Date.now()
    }, ACTIVITY_THROTTLE_MS)

    for (const event of ACTIVITY_EVENTS) {
      window.addEventListener(event, recordActivity, { passive: true })
    }

    return () => {
      for (const event of ACTIVITY_EVENTS) {
        window.removeEventListener(event, recordActivity)
      }
    }
  }, [])

  return {
    getLastActivityAt: () => lastActivityAtRef.current
  }
}
