'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import type { ReactNode } from 'react'
import { useEffect } from 'react'

import { IalGuard } from '@/features/auth'

/**
 * Layout for the standalone card replacement flow.
 * Guards against missing `case` query param and enforces IAL1+ via OIDC step-up
 * (required so the household address is available for mailing the replacement card).
 */
export default function CardReplaceLayout({ children }: { children: ReactNode }) {
  const searchParams = useSearchParams()
  const router = useRouter()
  const caseParam = searchParams.get('case')

  useEffect(() => {
    if (!caseParam) {
      router.replace('/dashboard')
    }
  }, [caseParam, router])

  if (!caseParam) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading...</span>
      </div>
    )
  }

  return <IalGuard>{children}</IalGuard>
}
