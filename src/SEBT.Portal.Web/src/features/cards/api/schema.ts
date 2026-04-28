import { z } from 'zod'

export const RequestCardReplacementSchema = z.object({
  caseIds: z.array(z.string()).min(1, 'At least one case ID is required.')
})

export type RequestCardReplacementRequest = z.infer<typeof RequestCardReplacementSchema>
