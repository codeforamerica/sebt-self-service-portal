'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

import { getDeviceSessionToken, initializeDeviceIntelligence } from './di-adapter'

/**
 * Hook that initializes the Socure Device Intelligence SDK and provides
 * a method to retrieve the session token. Initialize early (e.g., on the
 * ID proofing page) so the SDK has time to collect device signals before
 * the user submits the form.
 */
export function useDeviceIntelligence(sdkKey: string | undefined) {
  const [ready, setReady] = useState(false)
  const initAttempted = useRef(false)

  useEffect(() => {
    if (!sdkKey || initAttempted.current) return
    initAttempted.current = true

    initializeDeviceIntelligence(sdkKey)
      .then(() => setReady(true))
      .catch(() => {
        // DI is best-effort. If it fails, ID proofing still works
        // (backend falls back to config token or sends null).
      })
  }, [sdkKey])

  const getToken = useCallback(async (): Promise<string | null> => {
    if (!ready) return null
    return getDeviceSessionToken()
  }, [ready])

  return { ready, getToken }
}
