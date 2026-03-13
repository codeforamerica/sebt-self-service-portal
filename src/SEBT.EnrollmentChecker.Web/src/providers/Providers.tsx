'use client'

// i18n must be initialized before any component that uses useTranslation renders.
// This side-effect import runs when the client module loads in the browser.
import '@/lib/i18n-init'

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
        {children}
      </I18nProvider>
    </QueryClientProvider>
  )
}
