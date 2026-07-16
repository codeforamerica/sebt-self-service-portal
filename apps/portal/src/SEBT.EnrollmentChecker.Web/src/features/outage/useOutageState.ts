import { useCheckerFeatures } from '../maintenance/hooks/useCheckerFeatures'

export interface OutageState {
  /** True when the outage page should replace all checker routes. */
  outageActive: boolean
  /** True until the first successful features fetch — no last-known state exists yet. */
  isPending: boolean
}

/**
 * Derives the checker's outage page state from the shared features poll.
 *
 * Deliberately different staleness policy from the maintenance banner: the banner
 * fails closed (hides) when polls go stale, because wrongly showing an ops notice
 * is worse than hiding one. For the outage page the harm is inverted — un-showing
 * it mid-outage drops users onto a form whose submissions will error, which is
 * exactly what the page exists to prevent. React Query keeps the last successful
 * payload in `data` across failed refetches, so reading `data` without a staleness
 * veto makes an active outage sticky until a fresh poll says otherwise, while a
 * last-known-inactive state stays inactive (fail closed) when polls fail.
 *
 * A response without `outagePage` (older API) counts as inactive.
 */
export function useOutageState(apiBaseUrl: string): OutageState {
  const { data, isPending } = useCheckerFeatures(apiBaseUrl)

  return {
    outageActive: data?.outagePage?.enabled === true,
    isPending
  }
}
