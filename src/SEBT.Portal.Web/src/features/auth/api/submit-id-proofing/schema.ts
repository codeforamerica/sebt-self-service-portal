import { z } from 'zod'

// The ID types the user can provide for identity proofing.
// 'none' is a UI-only sentinel — the API receives null when the user selects "none of the above".
export const IdTypeSchema = z.enum(['snapAccountId', 'snapPersonId', 'medicaidId', 'ssn', 'itin'])
export type IdType = z.infer<typeof IdTypeSchema>

export const SubmitIdProofingRequestSchema = z.object({
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

export type SubmitIdProofingRequest = z.infer<typeof SubmitIdProofingRequestSchema>
