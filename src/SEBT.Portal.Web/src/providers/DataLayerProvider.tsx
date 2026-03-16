'use client'

import { useEffect, useRef, type ReactNode } from 'react'

import { DataLayer } from '@/lib/data-layer'

interface DataLayerProviderProps {
  children: ReactNode
}

/**
 * Initializes the vendor-agnostic data layer and binds it to window.digitalData.
 * Must be rendered client-side. Initializes once and persists across navigations.
 */
export function DataLayerProvider({ children }: DataLayerProviderProps) {
  const initialized = useRef(false)

  useEffect(() => {
    if (initialized.current) return
    initialized.current = true

    new DataLayer('digitalData')
  }, [])

  return <>{children}</>
}
