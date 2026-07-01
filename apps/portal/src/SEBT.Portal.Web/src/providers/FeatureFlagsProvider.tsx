'use client'

import { FeatureFlagsContext, useFeatureFlags } from '@/features/feature-flags'

import type { FeatureFlagsProviderProps } from './types'

/**
 * Provider component that fetches feature flags from the backend
 * and makes them available throughout the app via context.
 *
 * Must be used within a QueryProvider as it uses TanStack Query.
 */
export function FeatureFlagsProvider({ children }: FeatureFlagsProviderProps) {
  const { data: flags = {}, isLoading, isError } = useFeatureFlags()

  return (
    <FeatureFlagsContext.Provider value={{ flags, isLoading, isError }}>
      {children}
    </FeatureFlagsContext.Provider>
  )
}
