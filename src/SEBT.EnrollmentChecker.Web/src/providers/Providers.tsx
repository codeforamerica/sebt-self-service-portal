'use client'

import { EnrollmentProvider } from '@/features/enrollment/context/EnrollmentContext'
import { namespaces, stateResources } from '@/lib/generated-locale-resources'
import { DataLayerProvider } from '@sebt/analytics'
import { initI18n, type StateResources } from '@sebt/design-system/src/lib/i18n'
import { I18nProvider } from '@sebt/design-system/src/providers/I18nProvider'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useRef, useState, type ReactNode } from 'react'

const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'co').toLowerCase()

export function Providers({ children }: { children: ReactNode }) {
  // Initialize i18n once, lazily inside the component to avoid
  // calling initReactI18next during server-side module evaluation
  const i18nInitialized = useRef(false)
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
    <DataLayerProvider application="sebt-enrollment-checker">
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
