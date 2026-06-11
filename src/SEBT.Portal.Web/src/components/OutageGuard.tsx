'use client'

import { useFeatureFlag, useFeatureFlagsStatus } from '@/features/feature-flags'
import { readCachedOutageFlag, writeCachedOutageFlag } from '@/features/outage/outageFlagCache'
import { usePathname, useRouter } from 'next/navigation'
import { useEffect, useState, type ReactNode } from 'react'

export const OUTAGE_PATH = '/outage'

interface OutageGuardProps {
  children: ReactNode
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
  const [cachedOutageEnabled] = useState(() => readCachedOutageFlag())

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
