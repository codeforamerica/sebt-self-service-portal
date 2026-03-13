'use client'

import '@/lib/i18n-init'

import { EnrollmentProvider } from '@/features/enrollment/context/EnrollmentContext'
import { I18nProvider } from '@sebt/design-system'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'

export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(() => new QueryClient({
    defaultOptions: {
      queries: { retry: 1, staleTime: 60_000 }
    }
  }))

  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <EnrollmentProvider>
          {children}
        </EnrollmentProvider>
      </I18nProvider>
    </QueryClientProvider>
  )
}
