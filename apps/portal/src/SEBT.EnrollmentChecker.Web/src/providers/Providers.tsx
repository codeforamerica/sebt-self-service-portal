'use client'

import { EnrollmentProvider } from '@/features/enrollment/context/EnrollmentContext'
import { enrollmentCheckerRoutes } from '@/lib/analytics-routes'
import { namespaces, stateResources } from '@/lib/generated-locale-resources'
import { DataLayerProvider } from '@sebt/analytics'
import type { StateResources } from '@sebt/design-system'
import { initI18n, I18nProvider } from '@sebt/design-system/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useRef, useState, type ReactNode } from 'react'

const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'co').toLowerCase()

export function Providers({ children }: { children: ReactNode }) {
  // Initialize i18n once, lazily inside the component to avoid
  // calling initReactI18next during server-side module evaluation
  const i18nInitialized = useRef(false)
  // eslint-disable-next-line react-hooks/refs
  if (!i18nInitialized.current) {
    initI18n(stateResources as StateResources, namespaces, state)
    i18nInitialized.current = true
  }

  const [queryClient] = useState(() => new QueryClient({
    defaultOptions: {
      queries: { retry: 1, staleTime: 60_000 }
    }
  }))

  return (
    <DataLayerProvider application="sebt-enrollment-checker" routes={enrollmentCheckerRoutes}>
      <QueryClientProvider client={queryClient}>
        <I18nProvider>
          <EnrollmentProvider>
            {children}
          </EnrollmentProvider>
        </I18nProvider>
      </QueryClientProvider>
    </DataLayerProvider>
  )
}
