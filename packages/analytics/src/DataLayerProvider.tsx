'use client'

import { usePathname } from 'next/navigation'
import { useEffect, useLayoutEffect, useRef, type ReactNode } from 'react'

import { DataLayer } from './data-layer'
import { deriveEnvironmentFromHost } from './environment'
import { CTA_CLICK } from './events'
import { initWebVitals } from './web-vitals'

export interface PageContext {
  /** Logical page title */
  name: string
  /** High-level flow this page belongs to (e.g., "auth", "dashboard", "address_update") */
  flow: string
  /** Step index or logical step name within the flow */
  step: string
}

/** Map of pathname to page context for automatic page load tracking. */
export type RoutePageContextMap = Record<string, PageContext>

interface DataLayerProviderProps {
  /** Application identifier set on page.application (e.g., "sebt-portal", "sebt-enrollment-checker") */
  application: string
  /**
   * Environment name (e.g., "dev", "staging", "production"). Optional — when omitted,
   * it falls back to NEXT_PUBLIC_ENVIRONMENT, and then to a value derived from the
   * hostname at runtime (see {@link deriveEnvironmentFromHost}).
   */
  environment?: string
  /** Map of pathname to page context. When navigation occurs, matching context is set and pageLoad() fires. */
  routes?: RoutePageContextMap
  children: ReactNode
}

/**
 * Initializes the vendor-agnostic data layer and binds it to window.digitalData.
 * Automatically tracks page views on navigation and CTA clicks via delegation.
 * Must be rendered client-side. Initializes once and persists across navigations.
 */
export function DataLayerProvider({
  application,
  environment,
  routes,
  children
}: DataLayerProviderProps) {
  const initialized = useRef(false)

  // useLayoutEffect fires before any useEffect in the tree (including children
  // like PageTracker). This guarantees window.digitalData is initialized before
  // PageTracker.useEffect runs and checks for it — without this, PageTracker
  // would always see an uninitialized data layer on mount and skip the first
  // page_load, because React fires children's effects before parents' effects.
  useLayoutEffect(() => {
    if (initialized.current) return
    initialized.current = true

    if (!window.digitalData?.initialized) {
      new DataLayer('digitalData')
    }

    window.digitalData!.page.set('application', application)
    window.digitalData!.page.set('entry_source', application.replace('sebt-', '').replace(/-/g, '_'))
    window.digitalData!.page.set(
      'environment',
      environment ??
        process.env.NEXT_PUBLIC_ENVIRONMENT ??
        deriveEnvironmentFromHost(window.location.hostname)
    )
  }, [application, environment])

  return (
    <>
      <PageTracker routes={routes} />
      <CtaTracker />
      <WebVitalsTracker />
      {children}
    </>
  )
}

/** Fires a page_load event on every client-side navigation via the dedicated pageLoad() API. */
function PageTracker({ routes }: { routes: RoutePageContextMap | undefined }) {
  const pathname = usePathname()
  const routesRef = useRef(routes)
  routesRef.current = routes

  useEffect(() => {
    if (typeof window === 'undefined' || !window.digitalData?.initialized) return

    // Defer to let Next.js update document.title after navigation
    const raf = requestAnimationFrame(() => {
      const dl = window.digitalData!
      const lang = document.documentElement.lang || 'en'

      dl.page.set('language', lang)
      dl.page.set('locale', `${lang}_US`)

      // Set route-specific context or fall back to document.title.
      // Always clear flow/step in the fallback so stale values from a prior
      // matched route don't bleed into the next page_load event.
      const ctx = routesRef.current?.[pathname]
      if (ctx) {
        dl.page.set('name', ctx.name)
        dl.page.set('flow', ctx.flow)
        dl.page.set('step', ctx.step)
      } else {
        dl.page.set('name', document.title)
        dl.page.set('flow', '')
        dl.page.set('step', '')
      }

      dl.pageLoad()
    })

    return () => cancelAnimationFrame(raf)
  }, [pathname])

  return null
}

/** Fires cta_click events via delegated click listener on [data-analytics-cta] elements. */
function CtaTracker() {
  useEffect(() => {
    if (typeof window === 'undefined') return

    function handleClick(event: MouseEvent) {
      if (!(event.target instanceof Element)) return
      const target = event.target.closest('[data-analytics-cta]')
      if (!target || !window.digitalData?.initialized) return

      const ctaId = target.getAttribute('data-analytics-cta') || target.id || undefined
      const ctaTarget = target.getAttribute('aria-label') || target.textContent?.trim() || undefined
      // Opt-in: tag external destinations (phone, mailto, third-party links) with
      // data-analytics-cta-destination-type so analytics can split internal nav
      // from outbound interactions. Internal CTAs leave the attribute off.
      const ctaDestinationType =
        target.getAttribute('data-analytics-cta-destination-type') || undefined

      window.digitalData!.trackEvent(CTA_CLICK, {
        ...(ctaTarget && { cta_target: ctaTarget }),
        ...(ctaId && { cta_id: ctaId }),
        ...(ctaDestinationType && { cta_destination_type: ctaDestinationType })
      })
    }

    document.addEventListener('click', handleClick)
    return () => document.removeEventListener('click', handleClick)
  }, [])

  return null
}

/**
 * Subscribes Web Vitals reporting once per page lifetime. Subscriptions intentionally live
 * until the page unloads — CLS/INP only finalize at page-hide — so no cleanup is returned
 * from the effect. The ref guard keeps StrictMode's double-mount and any provider re-render
 * from double-subscribing.
 */
function WebVitalsTracker() {
  const subscribed = useRef(false)

  useEffect(() => {
    if (subscribed.current) return
    if (typeof window === 'undefined' || !window.digitalData?.initialized) return
    subscribed.current = true

    initWebVitals(window.digitalData)
  }, [])

  return null
}
