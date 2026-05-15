import { z } from 'zod'

export const RequestOtpRequestSchema = z.object({
  email: z.email({ message: 'Invalid email address' }),
  locale: z.string()
})

export type RequestOtpRequest = z.infer<typeof RequestOtpRequestSchema>
