import { z } from 'zod'

/**
 * Zod schema for the address update request body.
 * Mirrors the backend UpdateAddressRequest DTO.
 */
export const UpdateAddressRequestSchema = z.object({
  streetAddress1: z.string().min(1, 'Street address is required.'),
  streetAddress2: z.string().optional(),
  city: z.string().min(1, 'City is required.'),
  state: z.string().min(1, 'State is required.'),
  postalCode: z
    .string()
    .min(1, 'Postal code is required.')
    .regex(/^\d{5}(-\d{4})?$/, 'Postal code must be a valid 5- or 9-digit ZIP code.')
})

export type UpdateAddressRequest = z.infer<typeof UpdateAddressRequestSchema>

/**
 * Stub interface for address validation service (frontend side).
 * Replace with real Smarty integration when DC-160 is implemented.
 */
export interface AddressValidationResult {
  isValid: boolean
  suggestedAddress?: UpdateAddressRequest
  errorMessage?: string
}
