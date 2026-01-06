import { z } from 'zod'

export const ValidateOtpRequestSchema = z.object({
  email: z.email({ message: 'Invalid email address' }),
  otp: z.string().regex(/^\d{6}$/, 'OTP must be 6 digits')
})

export const ValidateOtpResponseSchema = z.object({
  token: z.string()
})

export type ValidateOtpRequest = z.infer<typeof ValidateOtpRequestSchema>
export type ValidateOtpResponse = z.infer<typeof ValidateOtpResponseSchema>
