import { createContext } from 'react'

import type { FeatureFlagsResponse } from '../api'

export interface FeatureFlagsContextValue {
  /** Dictionary of feature flag names to boolean values */
  flags: FeatureFlagsResponse
  /** Whether the feature flags are currently being loaded */
  isLoading: boolean
  /** Whether there was an error loading feature flags */
  isError: boolean
}

/**
 * Context for feature flags.
 * Provides access to feature flag values throughout the app.
 */
export const FeatureFlagsContext = createContext<FeatureFlagsContextValue | null>(null)
