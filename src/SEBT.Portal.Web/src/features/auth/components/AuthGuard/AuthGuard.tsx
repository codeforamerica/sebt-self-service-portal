'use client'

import { useAuth } from '@/features/auth'
import { useRouter } from 'next/navigation'
import { useEffect, useState, type ReactNode } from 'react'

interface AuthGuardProps {
  children: ReactNode
}

/**
 * AuthGuard protects routes that require authentication.
 * Redirects to /login if user is not authenticated.
 * Waits for client-side hydration before checking auth to avoid
 * redirect on page refresh when token exists in sessionStorage.
 */
export function AuthGuard({ children }: AuthGuardProps) {
  const { isAuthenticated, isLoading } = useAuth()
  const router = useRouter()
  const [isHydrated, setIsHydrated] = useState(false)

  // Wait for hydration before checking auth
  // This is a standard pattern for detecting client-side hydration
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => setIsHydrated(true), [])

  useEffect(() => {
    if (isHydrated && !isLoading && !isAuthenticated) {
      router.replace('/login')
    }
  }, [isHydrated, isAuthenticated, isLoading, router])

  // Show nothing while hydrating, loading, or redirecting
  if (!isHydrated || isLoading || !isAuthenticated) {
    return null
  }

  return <>{children}</>
}
