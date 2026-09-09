import { z } from 'zod'

/**
 * Schema for feature flags response from the backend API.
 * Returns a dictionary of feature flag names to boolean values.
 *
 * @example
 * {
 *   "enable_enrollment_status": true,
 *   "enable_spanish_support": true,
 *   "enable_card_replacement": false
 * }
 */
export const FeatureFlagsResponseSchema = z.record(z.string(), z.boolean())

export type FeatureFlagsResponse = z.infer<typeof FeatureFlagsResponseSchema>
