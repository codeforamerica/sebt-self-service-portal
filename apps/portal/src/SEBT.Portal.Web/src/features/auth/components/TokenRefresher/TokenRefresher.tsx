'use client'

import { useCallback } from 'react'

import { useRefreshToken } from '../../api'
import { useAuth } from '../../context'
import { useSessionRefresh, useUserActivity } from '../../idle'

/**
 * Idle window that gates the refresh.
 *
 * Mirrors the server's `JwtSettings.ExpirationMinutes` (15 min sliding). If the
 * user has been inactive for at least this long when the refresh timer fires,
 * the cookie is allowed to lapse — the next API call returns 401 and the
 * existing 401 handler redirects to /login.
 *
 * Per OWASP Session Management; aligns with NIST SP 800-63B IAL2 (≤30 min idle).
 */
const IDLE_THRESHOLD_MS = 15 * 60 * 1000

/**
 * Composes activity tracking with expiry-driven scheduling. The actual logic
 * lives in {@link useUserActivity} and {@link useSessionRefresh} — this component
 * is only here to wire `useAuth` and `useRefreshToken` to those hooks and to
 * mount inside the authenticated layout.
 */
export function TokenRefresher() {
  const { session, login } = useAuth()
  const { mutate } = useRefreshToken()
  const { getLastActivityAt } = useUserActivity()

  const refresh = useCallback(() => {
    mutate(undefined, {
      onSuccess: () => {
        // Pick up rotated cookie's new expiresAt and any updated claims.
        void login()
      }
    })
  }, [mutate, login])

  useSessionRefresh({
    expiresAt: session?.expiresAt ?? null,
    absoluteExpiresAt: session?.absoluteExpiresAt ?? null,
    getLastActivityAt,
    idleThresholdMs: IDLE_THRESHOLD_MS,
    refresh
  })

  return null
}
