import { z } from 'zod'

export const ValidateOtpRequestSchema = z.object({
  email: z.email(),
  otp: z.string().regex(/^\d{6}$/)
})

export type ValidateOtpRequest = z.infer<typeof ValidateOtpRequestSchema>
