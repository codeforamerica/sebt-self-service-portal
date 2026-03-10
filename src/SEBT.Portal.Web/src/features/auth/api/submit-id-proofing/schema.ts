import { z } from 'zod'

// The ID types the user can provide for identity proofing.
// 'none' is a UI-only sentinel — the API receives null when the user selects "none of the above".
export const IdTypeSchema = z.enum(['snapAccountId', 'snapPersonId', 'medicaidId', 'ssn', 'itin'])
export type IdType = z.infer<typeof IdTypeSchema>

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
    idValue: z.string().nullable()
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
      }
    }
  })

export type SubmitIdProofingRequest = z.infer<typeof SubmitIdProofingRequestSchema>

// Response contract for the id-proofing endpoint.
// The backend maps its internal IdProofingStatus to this frontend-friendly shape.
// 'documentVerificationRequired' triggers the Socure DocV flow (DC-137).
export const IdProofingResultSchema = z.enum(['matched', 'failed', 'documentVerificationRequired'])
export type IdProofingResult = z.infer<typeof IdProofingResultSchema>

export const SubmitIdProofingResponseSchema = z.object({
  result: IdProofingResultSchema,
  challengeId: z.string().optional(),
  allowIdRetry: z.boolean().optional(),
  canApply: z.boolean().optional(),
  offboardingReason: z.string().optional()
})
export type SubmitIdProofingResponse = z.infer<typeof SubmitIdProofingResponseSchema>
