import type { HouseholdData } from './schema'
import { useHouseholdData } from './useHouseholdData'

/**
 * Convenience hook for components that only render when household data is guaranteed loaded.
 *
 * DashboardContent handles loading/error states before rendering children,
 * so child components can safely assume data is available. This hook
 * eliminates prop drilling by letting children access the cached query directly.
 *
 * TanStack Query deduplicates by query key — multiple components calling
 * useHouseholdData() share the same cached data with no extra network requests.
 *
 * @throws Error if called before data is loaded (indicates incorrect component tree)
 */
export function useRequiredHouseholdData(): HouseholdData {
  const { data } = useHouseholdData()
  if (!data) {
    throw new Error(
      'useRequiredHouseholdData must only be used in components rendered after data is loaded'
    )
  }
  return data
}
