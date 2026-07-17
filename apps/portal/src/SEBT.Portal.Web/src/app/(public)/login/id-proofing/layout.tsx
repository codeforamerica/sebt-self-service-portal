'use client'

import { AuthGuard } from '@/features/auth'
import { usePathname } from 'next/navigation'
import type { ReactNode } from 'react'

interface IdProofingLayoutProps {
  children: ReactNode
}

/**
 * Protects all id-proofing routes (/login/id-proofing/*) except off-boarding,
 * which must remain reachable when the OIDC step-up round-trip loses the
 * portal session.
 */
export default function IdProofingLayout({ children }: IdProofingLayoutProps) {
  const pathname = usePathname()
  if (pathname.startsWith('/login/id-proofing/off-boarding')) {
    return <>{children}</>
  }
  return <AuthGuard>{children}</AuthGuard>
}
