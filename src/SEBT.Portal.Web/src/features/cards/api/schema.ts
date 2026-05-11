import { z } from 'zod'

export const CaseRefSchema = z.object({
  summerEbtCaseId: z.string().min(1, 'summerEbtCaseId is required.'),
  applicationId: z.string().nullable().optional(),
  applicationStudentId: z.string().nullable().optional()
})

export type CaseRef = z.infer<typeof CaseRefSchema>

export const RequestCardReplacementSchema = z.object({
  caseRefs: z.array(CaseRefSchema).min(1, 'At least one case reference is required.')
})

export type RequestCardReplacementRequest = z.infer<typeof RequestCardReplacementSchema>
