'use client'

import { useCallback, useEffect, useRef } from 'react'

import { useRefreshToken } from '../../api'
import { useAuth } from '../../context'

// Refresh token every 10 minutes while authenticated
const REFRESH_INTERVAL_MS = 10 * 60 * 1000

/**
 * TokenRefresher proactively refreshes the JWT token while the user is authenticated.
 * This prevents token expiration during active sessions.
 * Should be rendered inside authenticated routes.
 */
export function TokenRefresher() {
  const { isAuthenticated, login } = useAuth()
  const { mutate } = useRefreshToken()
  const intervalRef = useRef<NodeJS.Timeout | null>(null)

  const doRefresh = useCallback(() => {
    mutate(undefined, {
      onSuccess: (result) => {
        // Update the stored token with the new one
        login(result.token)
      }
      // Error handling (401 redirect) is done in apiFetch
      // Other errors are silently ignored - we'll retry on next interval
    })
  }, [mutate, login])

  useEffect(() => {
    if (!isAuthenticated) {
      // Clear interval if not authenticated
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
        intervalRef.current = null
      }
      return
    }

    // Initial refresh on mount (when entering authenticated area)
    doRefresh()

    // Set up periodic refresh
    intervalRef.current = setInterval(doRefresh, REFRESH_INTERVAL_MS)

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current)
        intervalRef.current = null
      }
    }
  }, [isAuthenticated, doRefresh])

  // This component doesn't render anything
  return null
}
