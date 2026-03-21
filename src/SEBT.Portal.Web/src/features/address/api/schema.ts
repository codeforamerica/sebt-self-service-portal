import { z } from 'zod'

/** Validates a US ZIP code: 5 digits, optionally followed by a dash and 4 digits.
 * Possibly replaced by SMARTY integration
 */
export function isValidZip(value: string): boolean {
  if (value.length !== 5 && value.length !== 10) return false
  if (value.length === 10 && value.charAt(5) !== '-') return false

  for (let i = 0; i < value.length; i++) {
    if (i === 5) continue
    const ch = value.charAt(i)
    if (ch < '0' || ch > '9') return false
  }
  return true
}

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
    .refine(isValidZip, 'Postal code must be a valid 5- or 9-digit ZIP code.')
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
