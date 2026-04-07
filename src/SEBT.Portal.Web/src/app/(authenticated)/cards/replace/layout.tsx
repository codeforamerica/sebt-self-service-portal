'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import type { ReactNode } from 'react'
import { useEffect } from 'react'

/**
 * Layout for the standalone card replacement flow.
 * Guards against missing `app` query param (required to identify which application to replace).
 */
export default function CardReplaceLayout({ children }: { children: ReactNode }) {
  const searchParams = useSearchParams()
  const router = useRouter()
  const appParam = searchParams.get('app')

  useEffect(() => {
    if (!appParam) {
      router.replace('/dashboard')
    }
  }, [appParam, router])

  if (!appParam) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading...</span>
      </div>
    )
  }

  return <>{children}</>
}
