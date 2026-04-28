import { z } from 'zod'
/**
 * Form-level schema: the shape of data as it lives in the ChildForm UI.
 * Month/day/year are separate fields for the USWDS memorable-date pattern.
 */
// Validation error keys are i18n translation keys resolved at display time.
// The ChildForm component maps these keys via t() before showing to the user.
export const childFormSchema = z.object({
  // TODO: Use t('validation.firstNameRequired') once content key is added
  firstName: z.string().min(1, 'This is required').max(100),
  middleName: z.string().max(100).optional(),
  // TODO: Use t('validation.lastNameRequired') once content key is added
  lastName: z.string().min(1, 'This is required').max(100),
  // TODO: Use t('validation.monthRequired') once content key is added
  month: z.string().min(1, 'Select a month'),
  // TODO: Use t('validation.dayFormat') once content key is added
  day: z.string().regex(/^(0?[1-9]|[12][0-9]|3[01])$/, 'Provide a day using one or two numbers'),
  // TODO: Use t('validation.yearFormat') once content key is added
  year: z.string().regex(/\b(19|20)\d{2}\b/, 'Provide a year using four numbers'),
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
  const parts = dateOfBirth.split('-')
  // Strip leading zeros for natural display (e.g., "04" -> "4")
  return {
    month: String(parseInt(parts[1]!, 10)),
    day: String(parseInt(parts[2]!, 10)),
    year: parts[0]!
  }
}
