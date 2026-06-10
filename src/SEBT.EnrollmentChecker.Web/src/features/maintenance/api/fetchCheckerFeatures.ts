import { z } from 'zod'

export const checkerFeaturesSchema = z.object({
  maintenanceBanner: z.object({
    enabled: z.boolean(),
    // Per-language banner copy keyed by ISO language code, e.g. { en: '...', es: '...' }.
    // The copy is runtime configuration so it can change without a deployment.
    message: z.record(z.string(), z.string())
  })
})

export type CheckerFeatures = z.infer<typeof checkerFeaturesSchema>

/**
 * Fetches runtime feature state for the checker (maintenance banner toggle + copy).
 *
 * @param apiBaseUrl - SSG: portal Node server URL (NEXT_PUBLIC_API_BASE_URL).
 *                     SSR: '' (same-origin /api route handles it).
 */
export async function fetchCheckerFeatures(apiBaseUrl: string): Promise<CheckerFeatures> {
  const response = await fetch(`${apiBaseUrl}/api/enrollment/features`)
  if (!response.ok) {
    throw new Error(`Checker features request failed with status ${response.status}`)
  }
  return checkerFeaturesSchema.parse(await response.json())
}
