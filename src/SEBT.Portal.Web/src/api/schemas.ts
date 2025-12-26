import { z } from 'zod'

export const ApiErrorResponseSchema = z.union([
  z.object({
    error: z.string(),
    message: z.string().optional()
  }),
  z.object({
    error: z.string().optional(),
    message: z.string()
  })
])

export type ApiErrorResponse = z.infer<typeof ApiErrorResponseSchema>
