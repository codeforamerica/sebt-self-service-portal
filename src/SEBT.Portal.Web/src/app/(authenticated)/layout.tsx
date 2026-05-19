import { BetaBanner } from '@/components/BetaBanner'
import { AuthGuard, TokenRefresher } from '@/features/auth'
import { UserDataSync } from '@/hooks/useUserDataSync'
import { FeatureFlagsProvider } from '@/providers'
import type { ReactNode } from 'react'

interface AuthenticatedLayoutProps {
  children: ReactNode
}

export default function AuthenticatedLayout({ children }: AuthenticatedLayoutProps) {
  return (
    <AuthGuard>
      <FeatureFlagsProvider>
        <TokenRefresher />
        <UserDataSync />
        <BetaBanner />
        {children}
      </FeatureFlagsProvider>
    </AuthGuard>
  )
}
