'use client'

import { useEffect } from 'react'

export function AxeProvider({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    if (process.env.NODE_ENV === 'development') {
      import('@/lib/axe')
        .then(({ initAxe }) => {
          initAxe()
        })
        .catch((err) => {
          console.warn('Failed to initialize axe accessibility testing:', err)
        })
    }
  }, [])

  return <>{children}</>
}
