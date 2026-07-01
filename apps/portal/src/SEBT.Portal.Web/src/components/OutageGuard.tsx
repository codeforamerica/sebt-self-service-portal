'use client'

import { useFeatureFlag, useFeatureFlagsStatus } from '@/features/feature-flags'
import { readCachedOutageFlag, writeCachedOutageFlag } from '@/features/outage/outageFlagCache'
import { usePathname, useRouter } from 'next/navigation'
import { useEffect, useSyncExternalStore, type ReactNode } from 'react'

export const OUTAGE_PATH = '/outage'

interface OutageGuardProps {
  children: ReactNode
}

function subscribeToOutageFlagCache(callback: () => void) {
  window.addEventListener('storage', callback)
  return () => window.removeEventListener('storage', callback)
}

function getOutageFlagCacheSnapshot(): boolean | null {
  return readCachedOutageFlag()
}

function getOutageFlagCacheServerSnapshot(): boolean | null {
  return null
}

/**
 * Redirects all portal routes to the outage page when the outage_page_enabled
 * feature flag is on. State partners toggle the flag via appsettings or AppConfig.
 *
 * Uses sessionStorage to remember the last-known flag value. When cached true,
 * non-outage routes block immediately (no flash of login/dashboard). When cached
 * false or missing, children render while /features loads so normal routes are
 * not gated on every navigation.
 */
export function OutageGuard({ children }: OutageGuardProps) {
  const outageEnabled = useFeatureFlag('outage_page_enabled')
  const { isLoading } = useFeatureFlagsStatus()
  const pathname = usePathname()
  const router = useRouter()
  const isOutagePage = pathname === OUTAGE_PATH
  // useSyncExternalStore keeps SSR/hydration aligned (server snapshot is null) while
  // still reading sessionStorage on the client after hydration.
  const cachedOutageEnabled = useSyncExternalStore(
    subscribeToOutageFlagCache,
    getOutageFlagCacheSnapshot,
    getOutageFlagCacheServerSnapshot
  )

  const outageActiveWhileLoading = cachedOutageEnabled === true
  const outageActive = isLoading ? outageActiveWhileLoading : outageEnabled

  useEffect(() => {
    if (!isLoading) {
      writeCachedOutageFlag(outageEnabled)
    }
  }, [isLoading, outageEnabled])

  useEffect(() => {
    if (outageActive && !isOutagePage) {
      router.replace(OUTAGE_PATH)
      return
    }

    if (!isLoading && !outageEnabled && isOutagePage) {
      router.replace('/login')
    }
  }, [isLoading, outageActive, outageEnabled, isOutagePage, router])

  const hideForOutageRedirect = !isOutagePage && outageActive
  const hideForLoginRedirect = isOutagePage && !isLoading && !outageEnabled

  if (hideForOutageRedirect || hideForLoginRedirect) {
    return null
  }

  return <>{children}</>
}
