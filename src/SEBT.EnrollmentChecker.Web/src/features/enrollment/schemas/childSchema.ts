import { z } from 'zod'

/**
 * Form-level schema: the shape of data as it lives in the ChildForm UI.
 * Month/day/year are separate fields for the USWDS memorable-date pattern.
 */
export const childFormSchema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  middleName: z.string().max(100).optional(),
  lastName: z.string().min(1, 'Last name is required').max(100),
  month: z.string().min(1, 'Month is required'),
  day: z.string().regex(/^\d{1,2}$/, 'Day must be 1-2 digits'),
  year: z.string().regex(/^\d{4}$/, 'Year must be 4 digits'),
  schoolName: z.string().max(200).optional(),
  schoolCode: z.string().max(50).optional()
})

export type ChildFormValues = z.infer<typeof childFormSchema>

/** Compose month/day/year into an ISO date string (YYYY-MM-DD). */
export function toDateOfBirth(values: Pick<ChildFormValues, 'month' | 'day' | 'year'>): string {
  const mm = values.month.padStart(2, '0')
  const dd = values.day.padStart(2, '0')
  return `${values.year}-${mm}-${dd}`
}

/** Decompose an ISO date string into month/day/year for form population. */
export function fromDateOfBirth(dateOfBirth: string): { month: string; day: string; year: string } {
  const [year, month, day] = dateOfBirth.split('-')
  // Strip leading zeros for natural display (e.g., "04" -> "4")
  return {
    month: String(parseInt(month, 10)),
    day: String(parseInt(day, 10)),
    year
  }
}
