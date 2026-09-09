'use client'

import { useFeatureFlag, useFeatureFlagsStatus } from '@/features/feature-flags'
import { OutageGuard as SharedOutageGuard } from '@sebt/design-system'
import { type ReactNode } from 'react'

export { OUTAGE_PATH } from '@sebt/design-system'

const OUTAGE_FLAG_CACHE_KEY = 'sebt_outage_page_enabled'

interface OutageGuardProps {
  children: ReactNode
}

/**
 * Redirects all portal routes to the outage page when the outage_page_enabled feature flag is on.
 * State partners toggle the flag via appsettings or AppConfig, and a scheduled maintenance window
 * targeting the portal overrides it server-side.
 *
 * The portal reads the flag from the feature-flags context, which fetches /features once. The
 * enrollment checker resolves its own outage state differently; only the redirect behavior is
 * shared.
 */
export function OutageGuard({ children }: OutageGuardProps) {
  const outageEnabled = useFeatureFlag('outage_page_enabled')
  const { isLoading } = useFeatureFlagsStatus()

  return (
    <SharedOutageGuard
      outageActive={outageEnabled}
      isResolving={isLoading}
      offPath="/login"
      storageKey={OUTAGE_FLAG_CACHE_KEY}
    >
      {children}
    </SharedOutageGuard>
  )
}
