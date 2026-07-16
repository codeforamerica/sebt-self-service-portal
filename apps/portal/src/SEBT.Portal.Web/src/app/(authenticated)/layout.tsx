import { AuthGuard, TokenRefresher } from '@/features/auth'
import { UserDataSync } from '@/hooks/useUserDataSync'
import type { ReactNode } from 'react'

interface AuthenticatedLayoutProps {
  children: ReactNode
}

export default function AuthenticatedLayout({ children }: AuthenticatedLayoutProps) {
  return (
    <AuthGuard>
      <TokenRefresher />
      <UserDataSync />
      {children}
    </AuthGuard>
  )
}
