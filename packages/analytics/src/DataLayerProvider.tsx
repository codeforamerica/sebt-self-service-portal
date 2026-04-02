'use client'

import { usePathname } from 'next/navigation'
import { useEffect, useRef, type ReactNode } from 'react'

import { DataLayer } from './data-layer'
import { CTA_CLICK, PAGE_LOAD } from './events'

interface DataLayerProviderProps {
  /** Application identifier set on page.application (e.g., "sebt-portal", "sebt-enrollment-checker") */
  application: string
  /** Environment name (e.g., "dev", "staging", "production"). Defaults to NEXT_PUBLIC_ENVIRONMENT or "production". */
  environment?: string
  children: ReactNode
}

/**
 * Initializes the vendor-agnostic data layer and binds it to window.digitalData.
 * Automatically tracks page views on navigation and CTA clicks via delegation.
 * Must be rendered client-side. Initializes once and persists across navigations.
 */
export function DataLayerProvider({ application, environment, children }: DataLayerProviderProps) {
  const initialized = useRef(false)

  useEffect(() => {
    if (typeof window === 'undefined') return
    if (initialized.current) return
    initialized.current = true

    if (!window.digitalData?.initialized) {
      new DataLayer('digitalData')
    }

    window.digitalData!.page.set('application', application)
    window.digitalData!.page.set(
      'environment',
      environment ?? process.env.NEXT_PUBLIC_ENVIRONMENT ?? 'production'
    )
  }, [application, environment])

  return (
    <>
      <PageTracker />
      <CtaTracker />
      {children}
    </>
  )
}

/** Fires a page_load event on every client-side navigation. */
function PageTracker() {
  const pathname = usePathname()

  useEffect(() => {
    if (typeof window === 'undefined' || !window.digitalData?.initialized) return

    // Defer to let Next.js update document.title after navigation
    const raf = requestAnimationFrame(() => {
      const dl = window.digitalData!
      const lang = document.documentElement.lang || 'en'

      dl.page.set('name', document.title)
      dl.page.set('language', lang)
      dl.page.set('locale', `${lang}_US`)

      dl.trackEvent(PAGE_LOAD)
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

      window.digitalData!.trackEvent(CTA_CLICK, {
        ...(ctaTarget && { cta_target: ctaTarget }),
        ...(ctaId && { cta_id: ctaId })
      })
    }

    document.addEventListener('click', handleClick)
    return () => document.removeEventListener('click', handleClick)
  }, [])

  return null
}
