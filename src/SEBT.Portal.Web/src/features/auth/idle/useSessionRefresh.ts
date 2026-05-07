'use client'

import { useEffect } from 'react'

/**
 * How long before `expiresAt` to fire the refresh. Gives the request time to
 * complete before the cookie expires and avoids racing the server clock.
 */
const REFRESH_SAFETY_MARGIN_MS = 60 * 1000

/**
 * If the user has been inactive for at least this long when the refresh timer
 * fires, the session is allowed to lapse. The threshold matches the sliding
 * idle window — keeping it in sync with the server's `ExpirationMinutes` lets
 * the same activity that would have refreshed the cookie also drive the refresh.
 */
function isWithinIdleThreshold(lastActivityAt: number, idleThresholdMs: number): boolean {
  return Date.now() - lastActivityAt < idleThresholdMs
}

export interface UseSessionRefreshOptions {
  /** Sliding-cookie expiry (Unix epoch seconds), or null when no session is active. */
  expiresAt: number | null
  /** Absolute lifetime cap (Unix epoch seconds). Past this, scheduling halts. */
  absoluteExpiresAt: number | null
  /** Returns the timestamp (ms since epoch) of the user's most recent activity. */
  getLastActivityAt: () => number
  /** Idle threshold in milliseconds — refresh fires only if activity is within this window. */
  idleThresholdMs: number
  /** Triggers a session refresh (e.g., the `useRefreshToken` mutation). */
  refresh: () => void
}

/**
 * Schedules a single, expiry-driven session refresh. On each tick:
 *
 *   - if the absolute cap has been reached → do nothing (let the session lapse)
 *   - if the user has been idle past the threshold → do nothing
 *   - otherwise → call `refresh()`
 *
 * The hook reschedules whenever `expiresAt` changes (i.e., after each successful
 * refresh re-reads `/auth/status`).
 */
export function useSessionRefresh({
  expiresAt,
  absoluteExpiresAt,
  getLastActivityAt,
  idleThresholdMs,
  refresh
}: UseSessionRefreshOptions): void {
  useEffect(() => {
    if (expiresAt === null) {
      return
    }

    const idleFireAtMs = expiresAt * 1000 - REFRESH_SAFETY_MARGIN_MS
    // Fire whichever happens first: the sliding refresh point or the absolute cap.
    // When the absolute cap arrives sooner, the refresh request hits the cap on the
    // server, gets 401 from the bearer middleware, and the SPA's 401 handler
    // redirects to /login — instead of leaving the user on a dead page until they
    // click something.
    const absoluteFireAtMs =
      absoluteExpiresAt !== null ? absoluteExpiresAt * 1000 : Number.POSITIVE_INFINITY
    const fireAtMs = Math.min(idleFireAtMs, absoluteFireAtMs)
    const delayMs = Math.max(0, fireAtMs - Date.now())

    const timer = setTimeout(() => {
      // At the absolute cap the session is dead regardless of activity — fire the
      // refresh anyway so the server rejects, the bearer middleware clears the
      // cookie, and apiFetch's 401 handler redirects the user to /login. The
      // idle gate only applies at the sliding fire point, where skipping the
      // refresh lets an idle session lapse naturally.
      const reachedAbsoluteCap =
        absoluteExpiresAt !== null && Date.now() >= absoluteExpiresAt * 1000
      if (!reachedAbsoluteCap && !isWithinIdleThreshold(getLastActivityAt(), idleThresholdMs)) {
        return
      }
      refresh()
    }, delayMs)

    return () => {
      clearTimeout(timer)
    }
  }, [expiresAt, absoluteExpiresAt, getLastActivityAt, idleThresholdMs, refresh])
}
