import { useQuery } from '@tanstack/react-query'
import { fetchCheckerFeatures } from '../api/fetchCheckerFeatures'

// Poll so a tab the user is looking at picks up a maintenance toggle within about
// a minute (background tabs pause polling and catch up on focus). The payload is a
// tiny anonymous GET, and the backend itself only refreshes its config from AWS
// AppConfig every ~90s, so polling faster buys nothing.
const REFETCH_INTERVAL_MS = 60_000

// How long the last successful fetch stays trustworthy once polls start failing.
// Within this window the banner keeps showing the last-known state, so a transient
// blip can't blank a real maintenance notice; beyond it the banner hides (fails
// closed) rather than keep showing state ops may no longer control.
const STALE_AFTER_MS = 5 * REFETCH_INTERVAL_MS

export function useCheckerFeatures(apiBaseUrl: string) {
  const query = useQuery({
    queryKey: ['checker-features', apiBaseUrl],
    queryFn: ({ signal }) => fetchCheckerFeatures(apiBaseUrl, signal),
    refetchInterval: REFETCH_INTERVAL_MS
  })

  // Pure comparison of the query's own clocks: errorUpdatedAt is 0 until a fetch
  // fails and resets relevance on each failed poll, so this only turns true once
  // failures have outlasted the last success by the tolerance.
  const isStale = query.errorUpdatedAt - query.dataUpdatedAt > STALE_AFTER_MS

  return { ...query, isStale }
}
