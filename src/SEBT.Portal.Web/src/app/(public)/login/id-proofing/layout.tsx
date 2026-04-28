'use client'

import { AuthGuard } from '@/features/auth'
import type { ReactNode } from 'react'

interface IdProofingLayoutProps {
  children: ReactNode
}

/**
 * Protects all id-proofing routes (/login/id-proofing/*).
 * Users must be authenticated (via OTP or OIDC) before entering the
 * identity-proofing flow. Unauthenticated visitors are redirected to /login.
 */
export default function IdProofingLayout({ children }: IdProofingLayoutProps) {
  return <AuthGuard>{children}</AuthGuard>
}
