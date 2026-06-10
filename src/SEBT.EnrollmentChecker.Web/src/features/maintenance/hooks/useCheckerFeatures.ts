import { useQuery } from '@tanstack/react-query'
import { fetchCheckerFeatures } from '../api/fetchCheckerFeatures'

// Poll so an already-open tab picks up a maintenance toggle within about a minute.
// The payload is a tiny anonymous GET, and the backend itself only refreshes its
// config from AWS AppConfig every ~90s, so polling faster buys nothing.
const REFETCH_INTERVAL_MS = 60_000

export function useCheckerFeatures(apiBaseUrl: string) {
  return useQuery({
    queryKey: ['checker-features'],
    queryFn: () => fetchCheckerFeatures(apiBaseUrl),
    refetchInterval: REFETCH_INTERVAL_MS
  })
}
