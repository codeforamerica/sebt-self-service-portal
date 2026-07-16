import type { QueryClient } from '@tanstack/react-query'

import { HOUSEHOLD_DATA_QUERY_KEY_PREFIX } from './queryKeys'

/** Removes all cached household queries (every userId suffix). Call when portal identity changes. */
export function clearHouseholdQueryCache(queryClient: QueryClient): void {
  queryClient.removeQueries({ queryKey: HOUSEHOLD_DATA_QUERY_KEY_PREFIX })
}
