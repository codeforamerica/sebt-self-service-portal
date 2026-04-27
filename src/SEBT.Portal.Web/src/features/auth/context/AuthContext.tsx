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
 * Mirrors the validated GET /api/auth/status response, minus the always-true `isAuthorized` flag.
 */
export interface SessionInfo {
  email: string | null
  ial: string | null
  idProofingStatus: number | null
  idProofingCompletedAt: number | null
  idProofingExpiresAt: number | null
  isCoLoaded: boolean | null
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
      email: response.email ?? null,
      ial: response.ial ?? null,
      idProofingStatus: response.idProofingStatus ?? null,
      idProofingCompletedAt: response.idProofingCompletedAt ?? null,
      idProofingExpiresAt: response.idProofingExpiresAt ?? null,
      isCoLoaded: response.isCoLoaded ?? null
    }
  } catch (error) {
    // 401 means not logged in; anything else we also treat as unauthenticated
    // so the guard can redirect. Network failures will retry on next navigation.
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
