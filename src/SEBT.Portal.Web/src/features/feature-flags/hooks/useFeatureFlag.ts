'use client'

import { useContext } from 'react'

import { FeatureFlagsContext } from '../context'

/**
 * Hook to check if a specific feature flag is enabled.
 * Returns false for unknown flags or if used outside the provider.
 *
 * @param flagName - The name of the feature flag to check
 * @returns boolean indicating if the feature is enabled
 *
 * @example
 * ```tsx
 * function EnrollmentStatus() {
 *   const showEnrollmentStatus = useFeatureFlag('enable_enrollment_status')
 *
 *   if (!showEnrollmentStatus) {
 *     return null
 *   }
 *
 *   return <StatusCard />
 * }
 * ```
 */
export function useFeatureFlag(flagName: string): boolean {
  const context = useContext(FeatureFlagsContext)

  if (!context) {
    return false
  }

  // eslint-disable-next-line security/detect-object-injection -- flagName is a controlled string key, not user input
  return context.flags[flagName] ?? false
}

/**
 * Hook to check the loading state of feature flags.
 * Useful for showing loading indicators while flags are being fetched.
 *
 * @returns Object with isLoading and isError states
 */
export function useFeatureFlagsStatus(): { isLoading: boolean; isError: boolean } {
  const context = useContext(FeatureFlagsContext)

  if (!context) {
    return { isLoading: false, isError: false }
  }

  return { isLoading: context.isLoading, isError: context.isError }
}
