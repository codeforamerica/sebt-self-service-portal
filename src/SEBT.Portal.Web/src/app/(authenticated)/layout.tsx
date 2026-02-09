import { AuthGuard, TokenRefresher } from '@/features/auth'
import type { ReactNode } from 'react'

interface AuthenticatedLayoutProps {
  children: ReactNode
}

export default function AuthenticatedLayout({ children }: AuthenticatedLayoutProps) {
  return (
    <AuthGuard>
      <TokenRefresher />
      {children}
    </AuthGuard>
  )
}
