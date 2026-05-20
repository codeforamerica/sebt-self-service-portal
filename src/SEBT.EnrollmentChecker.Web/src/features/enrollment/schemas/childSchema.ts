import { z } from 'zod'

/**
 * Form-level schema: the shape of data as it lives in the ChildForm UI.
 * Month/day/year are separate fields for the USWDS memorable-date pattern.
 *
 * Names accept Latin letters with diacritics plus hyphens, apostrophes
 * (straight or curly), and whitespace. Diacritics and curly quotes are
 * normalized to ASCII before the request reaches CBMS — see sanitizeForCbms.
 */
const NAME_PATTERN = /^[\p{Script=Latin}\p{M}\-'‘’\s]+$/u

export const childFormSchema = z
  .object({
    // TODO replace with t('validation.nameLetters') once content key is added
    firstName: z
      .string()
      .min(1, "Enter child's first name")
      .regex(NAME_PATTERN, 'Names may only contain letters, spaces, hyphens, and apostrophes'),
    middleName: z.string().max(100).optional(),
    // TODO replace with t('validation.nameLetters') once content key is added
    lastName: z
      .string()
      .min(1, "Enter child's last name")
      .regex(NAME_PATTERN, 'Names may only contain letters, spaces, hyphens, and apostrophes'),
    // TODO: Use t('validation.selectMonth') once content key is added
    month: z.string().regex(/^(0?[1-9]|1[0-2])$/, 'Select a month'),
    // TODO: Use t('validation.enterDay') once content key is added
    day: z.string().regex(/^(0?[1-9]|[12][0-9]|3[01])$/, 'Provide a day using one or two numbers'),
    // TODO: Use t('validation.enterYear') once content key is added
    year: z.string().regex(/^(19|20)\d{2}$/, 'Provide a year using four numbers'),
    schoolName: z.string().max(200).optional(),
    schoolCode: z.string().max(50).optional()
  })
  // TODO: Use t('validation.enterValidBirthdate') once content key is added
  .refine(isValidBirthDateWithinWindow, {
    message: 'Enter a valid birth date within the last 100 years',
    path: ['day']
  })

export type ChildFormValues = z.infer<typeof childFormSchema>

function isValidBirthDateWithinWindow(values: {
  month: string
  day: string
  year: string
}): boolean {
  const m = parseInt(values.month, 10)
  const d = parseInt(values.day, 10)
  const y = parseInt(values.year, 10)
  const candidate = new Date(y, m - 1, d)
  const isRealCalendarDate =
    candidate.getFullYear() === y &&
    candidate.getMonth() === m - 1 &&
    candidate.getDate() === d
  if (!isRealCalendarDate) return false

  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const earliest = new Date(today.getFullYear() - 100, today.getMonth(), today.getDate())
  return candidate >= earliest && candidate <= today
}

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
