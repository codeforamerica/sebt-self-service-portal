import { z } from 'zod'

// ── Request ────────────────────────────────────────────────────────────────

export const childCheckApiRequestSchema = z.object({
  firstName: z.string(),
  lastName: z.string(),
  dateOfBirth: z.string(),
  schoolName: z.string().optional(),
  schoolCode: z.string().optional(),
  /** middleName is not a direct field — sent via additionalFields["MiddleName"] */
  additionalFields: z.record(z.string(), z.string()).optional()
})

export const enrollmentCheckRequestSchema = z.object({
  children: z.array(childCheckApiRequestSchema)
})

export type EnrollmentCheckRequest = z.infer<typeof enrollmentCheckRequestSchema>
export type ChildCheckApiRequest = z.infer<typeof childCheckApiRequestSchema>

// ── Response ───────────────────────────────────────────────────────────────

export const childCheckApiResponseSchema = z.object({
  checkId: z.string(),
  firstName: z.string(),
  lastName: z.string(),
  dateOfBirth: z.string(),
  // API returns: Match | NonMatch | PossibleMatch | Error
  status: z.string(),
  matchConfidence: z.number().optional().nullable(),
  eligibilityType: z.string().optional().nullable(),
  schoolName: z.string().optional().nullable(),
  statusMessage: z.string().optional().nullable()
})

export const enrollmentCheckResponseSchema = z.object({
  results: z.array(childCheckApiResponseSchema),
  message: z.string().optional().nullable()
})

export type EnrollmentCheckResponse = z.infer<typeof enrollmentCheckResponseSchema>
export type ChildCheckApiResponse = z.infer<typeof childCheckApiResponseSchema>

// ── Status mapping ─────────────────────────────────────────────────────────

export type DisplayStatus = 'enrolled' | 'notEnrolled' | 'error'

/**
 * Maps .NET API status strings to frontend display states.
 * PossibleMatch is server-side suppressed per spec, but treated as enrolled if received.
 */
export function mapApiStatus(apiStatus: string): DisplayStatus {
  switch (apiStatus) {
    case 'Match':
    case 'PossibleMatch':
      return 'enrolled'
    case 'NonMatch':
      return 'notEnrolled'
    default:
      return 'error'
  }
}
