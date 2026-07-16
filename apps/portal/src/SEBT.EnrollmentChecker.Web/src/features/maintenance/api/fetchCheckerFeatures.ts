import { z } from 'zod'

export const checkerFeaturesSchema = z.object({
  maintenanceBanner: z.object({
    enabled: z.boolean(),
    // Per-language banner copy keyed by ISO language code, e.g. { en: '...', es: '...' }.
    // The copy is runtime configuration so it can change without a deployment.
    message: z.record(z.string(), z.string())
  }),
  // Optional for deploy-order safety: the statically-hosted checker and the API deploy
  // independently, and a strict field here would fail the whole features parse (hiding
  // the maintenance banner too) against an API that doesn't send it yet. Missing means
  // the outage page is off.
  outagePage: z
    .object({
      enabled: z.boolean()
    })
    .optional()
})

export type CheckerFeatures = z.infer<typeof checkerFeaturesSchema>

// Mirrors the SSR proxy route's upstream bound. Without a timeout a hung connection
// parks the query in 'fetching' forever: no data, no error, and no further polls,
// because React Query won't start an interval refetch while one is still in flight.
const TIMEOUT_MS = 10_000

/**
 * Fetches runtime feature state for the checker (maintenance banner toggle + copy).
 *
 * @param apiBaseUrl - SSG: portal Node server URL (NEXT_PUBLIC_API_BASE_URL).
 *                     SSR: '' (same-origin /api route handles it).
 * @param signal - React Query's abort signal, so superseded or unmounted fetches cancel.
 */
export async function fetchCheckerFeatures(apiBaseUrl: string, signal?: AbortSignal): Promise<CheckerFeatures> {
  const url = `${apiBaseUrl}/api/enrollment/features`
  const timeoutSignal = AbortSignal.timeout(TIMEOUT_MS)
  const response = await fetch(url, {
    signal: signal ? AbortSignal.any([signal, timeoutSignal]) : timeoutSignal
  })
  if (!response.ok) {
    // The URL distinguishes "proxy broken" (same-origin) from "API broken" (absolute)
    // in a user's console capture.
    throw new Error(`Checker features request to ${url} failed with status ${response.status}`)
  }
  return checkerFeaturesSchema.parse(await response.json())
}
