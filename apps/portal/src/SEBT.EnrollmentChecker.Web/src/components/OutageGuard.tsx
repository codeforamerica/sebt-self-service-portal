'use client'

import { useOutageState } from '@/features/outage/useOutageState'
import { getEnrollmentConfig } from '@/lib/stateConfig'
// Direct subpath import avoids the @sebt/design-system barrel, which pulls react-i18next
// into the RSC layer via layout.tsx -> CheckerShell.
import {
  OUTAGE_PATH,
  OutageGuard as SharedOutageGuard
} from '@sebt/design-system/src/components/OutageGuard/OutageGuard'
import { type ReactNode } from 'react'

export { OUTAGE_PATH }

const OUTAGE_FLAG_CACHE_KEY = 'sebt_checker_outage_page_enabled'

interface OutageGuardProps {
  children: ReactNode
}

/**
 * Redirects all checker routes to the outage page while an outage is active, riding the existing
 * features poll (schedule windows targeting the checker, or the manual checker_outage_page_enabled
 * flag — resolved server-side).
 *
 * useOutageState keeps an active outage sticky through failed polls, unlike the maintenance banner,
 * which hides itself once its data goes stale. Dropping users onto a form whose submissions will
 * error is worse than leaving the outage page up a little too long.
 */
export function OutageGuard({ children }: OutageGuardProps) {
  const { apiBaseUrl } = getEnrollmentConfig()
  const { outageActive, isPending } = useOutageState(apiBaseUrl)

  return (
    <SharedOutageGuard
      outageActive={outageActive}
      isResolving={isPending}
      offPath="/"
      storageKey={OUTAGE_FLAG_CACHE_KEY}
    >
      {children}
    </SharedOutageGuard>
  )
}
