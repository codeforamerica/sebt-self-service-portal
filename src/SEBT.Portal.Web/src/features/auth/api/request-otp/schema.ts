import { z } from 'zod'

export const RequestOtpRequestSchema = z.object({
  email: z.email()
})

export type RequestOtpRequest = z.infer<typeof RequestOtpRequestSchema>
