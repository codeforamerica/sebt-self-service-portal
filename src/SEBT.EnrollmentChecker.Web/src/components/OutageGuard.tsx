'use client'

import { readCachedOutageFlag, writeCachedOutageFlag } from '@/features/outage/outageFlagCache'
import { useOutageState } from '@/features/outage/useOutageState'
import { getEnrollmentConfig } from '@/lib/stateConfig'
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
 * Redirects all checker routes to the outage page while an outage is active, riding
 * the existing features poll (schedule windows targeting the checker, or the manual
 * checker_outage_page_enabled flag — resolved server-side).
 *
 * Uses sessionStorage to remember the last-known outage state. When cached true,
 * non-outage routes block immediately (no flash of the form). When cached false or
 * missing, children render while the first features fetch is in flight, so normal
 * routes are not gated on every navigation. Once a fetch succeeds, the live value
 * governs — and stays sticky through failed polls (see useOutageState).
 */
export function OutageGuard({ children }: OutageGuardProps) {
  const { apiBaseUrl } = getEnrollmentConfig()
  const { outageActive: outageEnabled, isPending } = useOutageState(apiBaseUrl)
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
  const outageActive = isPending ? outageActiveWhileLoading : outageEnabled

  useEffect(() => {
    if (!isPending) {
      writeCachedOutageFlag(outageEnabled)
    }
  }, [isPending, outageEnabled])

  useEffect(() => {
    if (outageActive && !isOutagePage) {
      router.replace(OUTAGE_PATH)
      return
    }

    if (!isPending && !outageEnabled && isOutagePage) {
      router.replace('/')
    }
  }, [isPending, outageActive, outageEnabled, isOutagePage, router])

  const hideForOutageRedirect = !isOutagePage && outageActive
  const hideForLandingRedirect = isOutagePage && !isPending && !outageEnabled

  if (hideForOutageRedirect || hideForLandingRedirect) {
    return null
  }

  return <>{children}</>
}
