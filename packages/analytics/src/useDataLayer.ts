'use client'

import { useMemo } from 'react'

import type { DataLayerRoot } from './data-layer'

function dl(): DataLayerRoot | undefined {
  return typeof window !== 'undefined' ? window.digitalData : undefined
}

/**
 * Typed hook for interacting with the data layer from React components.
 * Returns stable function references (safe for useEffect deps).
 * No-ops during SSR or before the data layer initializes.
 */
export function useDataLayer() {
  return useMemo(
    () => ({
      trackEvent: (name: string, data?: Record<string, unknown>) => dl()?.trackEvent(name, data),
      pageLoad: (data?: Record<string, unknown>) => dl()?.pageLoad(data),
      setPageData: (path: string, value: unknown, scope?: string | string[]) =>
        dl()?.page.set(path, value, scope),
      setPageCategory: (path: string, value: unknown, scope?: string | string[]) =>
        dl()?.page.category.set(path, value, scope),
      setPageAttribute: (path: string, value: unknown, scope?: string | string[]) =>
        dl()?.page.attribute.set(path, value, scope),
      setUserData: (path: string, value: unknown, scope?: string | string[]) =>
        dl()?.user.set(path, value, scope),
      setUserProfile: (path: string, value: unknown, scope?: string | string[]) =>
        dl()?.user.profile.set(path, value, scope),
      get: (path: string, scope?: string, defaultValue?: unknown) =>
        dl()?.get(path, scope, defaultValue)
    }),
    []
  )
}
