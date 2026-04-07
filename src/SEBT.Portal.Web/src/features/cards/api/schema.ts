import { z } from 'zod'

export const RequestCardReplacementSchema = z.object({
  applicationNumbers: z.array(z.string()).min(1, 'At least one application number is required.')
})

export type RequestCardReplacementRequest = z.infer<typeof RequestCardReplacementSchema>
