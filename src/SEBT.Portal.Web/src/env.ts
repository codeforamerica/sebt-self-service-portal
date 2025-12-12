/**
 * Environment Variable Validation
 *
 * Type-safe environment variables with runtime validation.
 * Catches missing or invalid environment variables at build time.
 *
 * Usage:
 *   import { env } from '@/src/env';
 *   const state = env.NEXT_PUBLIC_STATE; // Type-safe!
 */
import { createEnv } from '@t3-oss/env-nextjs'
import { z } from 'zod'

export const env = createEnv({
  /**
   * Server-side environment variables
   * Not exposed to the client
   */
  server: {
    NODE_ENV: z.enum(['development', 'test', 'production']).optional()
  },

  /**
   * Client-side environment variables
   * Must be prefixed with NEXT_PUBLIC_
   */
  client: {
    NEXT_PUBLIC_STATE: z.enum(['dc', 'co'])
  },

  /**
   * Runtime environment variables
   * Map to process.env
   */
  runtimeEnv: {
    NODE_ENV: process.env.NODE_ENV,
    NEXT_PUBLIC_STATE: process.env.NEXT_PUBLIC_STATE
  },

  /**
   * Skip validation during build
   * Set to false for strict validation
   */
  skipValidation: !!process.env.SKIP_ENV_VALIDATION,

  /**
   * Makes it so empty strings are treated as undefined
   * Useful for optional environment variables
   */
  emptyStringAsUndefined: true
})
