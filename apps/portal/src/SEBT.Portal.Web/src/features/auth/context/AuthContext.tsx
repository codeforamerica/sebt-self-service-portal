'use client'

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from 'react'

import { ApiError, apiFetch } from '@/api/client'

import { AuthorizationStatusResponseSchema } from '../api/auth-status'

/**
 * Non-sensitive session claims the SPA needs for UI decisions.
 * The underlying JWT lives in an HttpOnly cookie and cannot be read by JavaScript.
 * Mirrors the validated GET /api/auth/status response, minus the `isAuthorized` flag
 * (an unauthenticated probe resolves to a null session instead).
 */
export interface SessionInfo {
  /** Stable, non-PII portal user UUID. Surfaced for analytics correlation. */
  userId: string | null
  email: string | null
  ial: string | null
  idProofingStatus: number | null
  idProofingCompletedAt: number | null
  idProofingExpiresAt: number | null
  isCoLoaded: boolean | null
  /** Unix epoch seconds when the sliding session cookie expires. */
  expiresAt: number | null
  /** Unix epoch seconds when the absolute session lifetime cap is reached. */
  absoluteExpiresAt: number | null
}

interface AuthContextValue {
  session: SessionInfo | null
  isAuthenticated: boolean
  isLoading: boolean
  /**
   * Fetches /auth/status and updates context with the current session (call after login/refresh).
   * Returns the freshly fetched session so callers can route based on its claims without
   * waiting for React state to flush.
   */
  login: () => Promise<SessionInfo | null>
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

async function fetchSession(): Promise<SessionInfo | null> {
  try {
    const response = await apiFetch('/auth/status', { schema: AuthorizationStatusResponseSchema })
    if (!response.isAuthorized) return null
    return {
      userId: response.userId ?? null,
      email: response.email ?? null,
      ial: response.ial ?? null,
      idProofingStatus: response.idProofingStatus ?? null,
      idProofingCompletedAt: response.idProofingCompletedAt ?? null,
      idProofingExpiresAt: response.idProofingExpiresAt ?? null,
      isCoLoaded: response.isCoLoaded ?? null,
      expiresAt: response.expiresAt ?? null,
      absoluteExpiresAt: response.absoluteExpiresAt ?? null
    }
  } catch (error) {
    // The API answers anonymous probes with 200 { isAuthorized: false }, but keep
    // treating a 401 as "not logged in" too — it still arrives from older API
    // deployments during a rollout window. Anything else is also treated as
    // unauthenticated so the guard can redirect; network failures retry on next
    // navigation.
    if (error instanceof ApiError && error.status !== 401) {
      console.warn('Failed to fetch auth session', error)
    }
    return null
  }
}

interface AuthProviderProps {
  children: ReactNode
}

/**
 * Tracks the current authenticated session. On mount, queries /auth/status using
 * the HttpOnly session cookie to determine who (if anyone) is logged in.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const [session, setSession] = useState<SessionInfo | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    fetchSession().then((result) => {
      if (!cancelled) {
        setSession(result)
        setIsLoading(false)
      }
    })
    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(async () => {
    const result = await fetchSession()
    setSession(result)
    return result
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      isAuthenticated: session !== null,
      isLoading,
      login
    }),
    [session, isLoading, login]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

/**
 * Hook to access auth context.
 * Must be used within an AuthProvider.
 */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
