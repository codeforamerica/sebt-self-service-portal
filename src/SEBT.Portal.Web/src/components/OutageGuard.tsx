'use client'

import { useFeatureFlag, useFeatureFlagsStatus } from '@/features/feature-flags'
import { usePathname, useRouter } from 'next/navigation'
import { useEffect, type ReactNode } from 'react'

export const OUTAGE_PATH = '/outage'

interface OutageGuardProps {
  children: ReactNode
}

/**
 * Redirects all portal routes to the outage page when the outage_page_enabled
 * feature flag is on. State partners toggle the flag via appsettings or AppConfig.
 */
export function OutageGuard({ children }: OutageGuardProps) {
  const outageEnabled = useFeatureFlag('outage_page_enabled')
  const { isLoading } = useFeatureFlagsStatus()
  const pathname = usePathname()
  const router = useRouter()
  const isOutagePage = pathname === OUTAGE_PATH

  useEffect(() => {
    if (isLoading) {
      return
    }

    if (outageEnabled && !isOutagePage) {
      router.replace(OUTAGE_PATH)
      return
    }

    if (!outageEnabled && isOutagePage) {
      router.replace('/login')
    }
  }, [isLoading, outageEnabled, isOutagePage, router])

  if (isLoading) {
    return null
  }

  if (outageEnabled && !isOutagePage) {
    return null
  }

  if (!outageEnabled && isOutagePage) {
    return null
  }

  return <>{children}</>
}
