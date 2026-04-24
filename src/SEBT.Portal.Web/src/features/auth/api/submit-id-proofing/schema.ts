import { z } from 'zod'

// The ID types the user can provide for identity proofing.
// 'none' is a UI-only sentinel — the API receives null when the user selects "none of the above".
export const IdTypeSchema = z.enum(['snapAccountId', 'snapPersonId', 'medicaidId', 'ssn', 'itin'])
export type IdType = z.infer<typeof IdTypeSchema>

// Phase 1 of DC-296: guard malformed national IDs and nonsense DOBs so they
// never reach the backend (and therefore Socure). The UI input strips non-digit
// characters on change, but this schema is the contract of last resort.
const SSN_ITIN_DIGIT_COUNT = 9
// Maximum plausible age. 120 years is the high end for a living person.
// TODO(DC-296 follow-up): revisit with product if we want a tighter bound.
const MAX_AGE_YEARS = 120
// Medicaid ID shape is intentionally NOT validated here. DC CSV advertises
// "7 or 8 digits" but Socure expects "4 or 9" — this is a separate open
// product/policy question tracked outside DC-296.

// Returns true when (month, day, year) describes a real calendar date. Rejects
// Feb 30, Feb 29 in non-leap years, month 13, etc. Relies on JS Date rollover
// behaviour: setting month=1 (Feb), day=30 produces March 2, so getMonth()
// no longer equals the input.
function isRealCalendarDate(year: number, month: number, day: number): boolean {
  if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)) return false
  if (month < 1 || month > 12) return false
  if (day < 1 || day > 31) return false
  const d = new Date(year, month - 1, day)
  return d.getFullYear() === year && d.getMonth() === month - 1 && d.getDate() === day
}

function validateDateOfBirth(
  dob: { month: string; day: string; year: string },
  ctx: z.RefinementCtx
): void {
  // Only run shape checks when all three fields are present. Empty-field
  // required-ness is enforced at the form layer with field-level errors.
  if (!dob.month || !dob.day || !dob.year) return

  const month = Number(dob.month)
  const day = Number(dob.day)
  const year = Number(dob.year)

  if (!isRealCalendarDate(year, month, day)) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['dateOfBirth'],
      message: 'dateOfBirth must be a real calendar date'
    })
    return
  }

  const dobDate = new Date(year, month - 1, day)
  const now = new Date()
  if (dobDate.getTime() > now.getTime()) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['dateOfBirth'],
      message: 'dateOfBirth cannot be in the future'
    })
    return
  }

  const oldestAllowed = new Date(now.getFullYear() - MAX_AGE_YEARS, now.getMonth(), now.getDate())
  if (dobDate.getTime() < oldestAllowed.getTime()) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ['dateOfBirth'],
      message: `dateOfBirth cannot be more than ${MAX_AGE_YEARS} years ago`
    })
  }
}

function validateIdValueShape(idType: IdType, idValue: string, ctx: z.RefinementCtx): void {
  if (idType === 'ssn' || idType === 'itin') {
    const digits = idValue.replace(/\D/g, '')
    if (digits.length !== SSN_ITIN_DIGIT_COUNT) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['idValue'],
        message: `${idType} must be exactly ${SSN_ITIN_DIGIT_COUNT} digits`
      })
    }
  }
  // snapAccountId / snapPersonId: excluded from Socure payload, no shape check here.
  // medicaidId: open product question, no shape check here.
}

export const SubmitIdProofingRequestSchema = z
  .object({
    dateOfBirth: z.object({
      month: z.string(),
      day: z.string(),
      year: z.string()
    }),
    // null when the user selects "none of the above"
    idType: IdTypeSchema.nullable(),
    // null when idType is null
    idValue: z.string().nullable(),
    // Device Intelligence session token from the Socure DI SDK (optional, best-effort)
    diSessionToken: z.string().nullable().optional()
  })
  .superRefine((data, ctx) => {
    if (data.idType === null && data.idValue !== null) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['idValue'],
        message: 'idValue must be null when idType is null'
      })
    }
    if (data.idType !== null) {
      const v = data.idValue
      if (v === null || v.trim() === '') {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ['idValue'],
          message: 'idValue is required when idType is not null'
        })
      } else {
        validateIdValueShape(data.idType, v, ctx)
      }
    }

    validateDateOfBirth(data.dateOfBirth, ctx)
  })

export type SubmitIdProofingRequest = z.infer<typeof SubmitIdProofingRequestSchema>

// Response contract for the id-proofing endpoint.
// The backend maps its internal IdProofingStatus to this frontend-friendly shape.
// 'documentVerificationRequired' triggers the Socure DocV flow (DC-137).
export const IdProofingResultSchema = z.enum(['matched', 'failed', 'documentVerificationRequired'])
export type IdProofingResult = z.infer<typeof IdProofingResultSchema>

export const SubmitIdProofingResponseSchema = z.object({
  result: IdProofingResultSchema,
  challengeId: z.string().nullable().optional(),
  allowIdRetry: z.boolean().nullable().optional(),
  canApply: z.boolean().nullable().optional(),
  offboardingReason: z.string().nullable().optional()
})
export type SubmitIdProofingResponse = z.infer<typeof SubmitIdProofingResponseSchema>
