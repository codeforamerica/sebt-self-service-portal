'use client'

import { usePathname, useRouter } from 'next/navigation'
import { useEffect, useMemo, useSyncExternalStore, type ReactNode } from 'react'
import { createOutageFlagCache } from '../../lib/outageFlagCache'

export const OUTAGE_PATH = '/outage'

export interface OutageGuardProps {
  /** Whether an outage is currently in effect for this app's surface. */
  outageActive: boolean
  /** True while the first resolution of `outageActive` is still in flight. */
  isResolving: boolean
  /** Where to send someone who lands on the outage page after the outage ends. */
  offPath: string
  /** sessionStorage key for this app's cached flag. Surfaces must not share one. */
  storageKey: string
  children: ReactNode
}

function subscribeToOutageFlagCache(callback: () => void) {
  window.addEventListener('storage', callback)
  return () => window.removeEventListener('storage', callback)
}

/**
 * Routes every page to the outage page while an outage is active, and back off it once the outage
 * ends. Headless: the caller resolves `outageActive` however it likes and this owns only the
 * redirect choreography.
 *
 * That split matters. The portal reads a feature flag it fetches once; the checker polls a separate
 * endpoint every minute and deliberately keeps showing the outage through failed polls. Sharing the
 * fetching would erase that difference. Sharing the choreography — the sessionStorage cache, the
 * hydration-safe read of it, and the two redirects — keeps one tested copy of the subtle part.
 *
 * The cache prevents a flash of content. When it says an outage was active, non-outage routes block
 * immediately rather than rendering a page the redirect is about to replace. When it says inactive,
 * or says nothing, children render while the first fetch is in flight, so a normal navigation is
 * never gated on the network.
 */
export function OutageGuard({
  outageActive: resolvedOutageActive,
  isResolving,
  offPath,
  storageKey,
  children
}: OutageGuardProps) {
  const cache = useMemo(() => createOutageFlagCache(storageKey), [storageKey])
  const pathname = usePathname()
  const router = useRouter()
  const isOutagePage = pathname === OUTAGE_PATH

  // useSyncExternalStore keeps SSR/hydration aligned (server snapshot is null) while
  // still reading sessionStorage on the client after hydration.
  const cachedOutageActive = useSyncExternalStore(
    subscribeToOutageFlagCache,
    cache.read,
    getServerSnapshot
  )

  const outageActive = isResolving ? cachedOutageActive === true : resolvedOutageActive

  useEffect(() => {
    if (!isResolving) {
      cache.write(resolvedOutageActive)
    }
  }, [cache, isResolving, resolvedOutageActive])

  useEffect(() => {
    if (outageActive && !isOutagePage) {
      router.replace(OUTAGE_PATH)
      return
    }

    if (!isResolving && !resolvedOutageActive && isOutagePage) {
      router.replace(offPath)
    }
  }, [isResolving, outageActive, resolvedOutageActive, isOutagePage, offPath, router])

  const hideForOutageRedirect = !isOutagePage && outageActive
  const hideForOffPathRedirect = isOutagePage && !isResolving && !resolvedOutageActive

  if (hideForOutageRedirect || hideForOffPathRedirect) {
    return null
  }

  return <>{children}</>
}

function getServerSnapshot(): boolean | null {
  return null
}
