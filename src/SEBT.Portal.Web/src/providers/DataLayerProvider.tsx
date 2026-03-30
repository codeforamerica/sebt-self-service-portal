'use client'

import { useEffect, type ReactNode } from 'react'

import { DataLayer } from '@/lib/data-layer'

interface DataLayerProviderProps {
  children: ReactNode
}

/**
 * Initializes the vendor-agnostic data layer and binds it to window.digitalData.
 * Must be rendered client-side. Initializes once and persists across navigations.
 */
export function DataLayerProvider({ children }: DataLayerProviderProps) {
  useEffect(() => {
    if (typeof window === 'undefined') return
    if (window.digitalData?.initialized) return

    new DataLayer('digitalData')
  }, [])

  return <>{children}</>
}
