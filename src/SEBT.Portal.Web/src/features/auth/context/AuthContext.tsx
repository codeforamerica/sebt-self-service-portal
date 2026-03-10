'use client'

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useSyncExternalStore,
  type ReactNode
} from 'react'

export const AUTH_TOKEN_KEY = 'auth_token'

interface AuthContextValue {
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (token: string) => void
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

interface AuthProviderProps {
  children: ReactNode
}

// External store for token to avoid useEffect setState issues
let tokenListeners: Array<() => void> = []

function getTokenSnapshot(): string | null {
  if (typeof window === 'undefined') {
    return null
  }
  return sessionStorage.getItem(AUTH_TOKEN_KEY)
}

/**
 * Persist token to sessionStorage and notify listeners. Called by login() and by OIDC callback.
 */
export function setAuthToken(newToken: string | null): void {
  setTokenExternal(newToken)
}

function getServerSnapshot(): string | null {
  return null
}

function subscribeToToken(callback: () => void): () => void {
  tokenListeners.push(callback)
  return () => {
    tokenListeners = tokenListeners.filter((l) => l !== callback)
  }
}

function setTokenExternal(newToken: string | null): void {
  if (newToken !== null) {
    sessionStorage.setItem(AUTH_TOKEN_KEY, newToken)
  } else {
    sessionStorage.removeItem(AUTH_TOKEN_KEY)
  }
  tokenListeners.forEach((listener) => listener())
}

/**
 * AuthProvider manages JWT token state for authenticated API requests.
 * Token is persisted in sessionStorage for security (cleared on browser close).
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const token = useSyncExternalStore(subscribeToToken, getTokenSnapshot, getServerSnapshot)

  const login = useCallback((newToken: string) => {
    setTokenExternal(newToken)
  }, [])

  const logout = useCallback(() => {
    setTokenExternal(null)
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      isAuthenticated: token !== null,
      // With useSyncExternalStore, the token is available synchronously
      isLoading: false,
      login,
      logout
    }),
    [token, login, logout]
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

/**
 * Get the current auth token from sessionStorage.
 * This is a synchronous helper for use in non-React code (e.g., apiFetch).
 * Returns null if no token is stored.
 */
export function getAuthToken(): string | null {
  if (typeof window === 'undefined') {
    return null
  }
  return sessionStorage.getItem(AUTH_TOKEN_KEY)
}

/**
 * Clear the auth token from sessionStorage.
 * This is a synchronous helper for use in non-React code (e.g., apiFetch 401 handling).
 * Notifies all listeners so React components update.
 */
export function clearAuthToken(): void {
  if (typeof window === 'undefined') {
    return
  }
  setTokenExternal(null)
}
