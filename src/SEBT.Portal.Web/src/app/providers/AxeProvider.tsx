'use client'

/**
 * AxeProvider - Client Component for accessibility monitoring
 */

import { useEffect } from 'react'

export function AxeProvider({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    if (process.env.NODE_ENV === 'development') {
      import('@/src/lib/axe').then(({ initAxe }) => {
        initAxe()
      })
    }
  }, [])

  return <>{children}</>
}
