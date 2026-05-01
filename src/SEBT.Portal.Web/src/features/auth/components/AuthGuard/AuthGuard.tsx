'use client'

import { CoLoadingScreen } from '@/components/CoLoadingScreen'
import { useAuth } from '@/features/auth'
import { useRouter } from 'next/navigation'
import { useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

interface AuthGuardProps {
  children: ReactNode
}

/**
 * AuthGuard protects routes that require authentication.
 * Redirects to /login if user is not authenticated.
 * Waits for client-side hydration before checking auth so that the initial
 * /auth/status fetch (sourced from the HttpOnly session cookie) has time to
 * resolve, avoiding a flash redirect for users with a valid session.
 */
export function AuthGuard({ children }: AuthGuardProps) {
  const { isAuthenticated, isLoading } = useAuth()
  const { t } = useTranslation('step-upProcessing')
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

  // While the auth-status fetch is in flight, CO shows an interstitial so the
  // user knows the dashboard is still loading. Other states keep the existing
  // blank-screen behavior until their content is wired up.
  if (!isHydrated || isLoading) {
    return (
      <CoLoadingScreen
        title={t('title', 'Please wait...')}
        message={t('body', 'Do not exit the page. Checking to see if we have enough information.')}
      />
    )
  }

  if (!isAuthenticated) {
    return null
  }

  return <>{children}</>
}
