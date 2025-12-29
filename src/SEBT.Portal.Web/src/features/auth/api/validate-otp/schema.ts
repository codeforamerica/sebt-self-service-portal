import { z } from 'zod'

export const ValidateOtpRequestSchema = z.object({
  email: z.email({ message: 'Invalid email address' }),
  otp: z.string().min(1, 'OTP is required')
})

export type ValidateOtpRequest = z.infer<typeof ValidateOtpRequestSchema>
