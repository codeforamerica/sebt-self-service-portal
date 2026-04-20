/**
 * Environment Variable Validation
 *
 * Type-safe environment variables with runtime validation.
 * Catches missing or invalid environment variables at build time.
 *
 * Usage:
 *   import { env } from '@/env';
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
    NODE_ENV: z.enum(['development', 'test', 'production']).optional(),
    BACKEND_URL: z.url().default('http://localhost:5280'),
    // Origin of the enrollment checker static site (e.g. https://dev.co.sebt-enrollment.codeforamerica.app).
    // When set, the portal returns CORS headers on /api/enrollment/* responses to allow
    // cross-origin requests from the SSG-deployed enrollment checker.
    ENROLLMENT_CHECKER_ORIGIN: z.url().optional()
    // OIDC secrets (CLIENT_SECRET, COMPLETE_LOGIN_SIGNING_KEY, etc.) moved to
    // .NET appsettings. The Next.js OIDC callback route was deleted — all OIDC exchange
    // and validation now happens server-side in OidcExchangeService.
  },

  /**
   * Client-side environment variables
   * Must be prefixed with NEXT_PUBLIC_
   */
  client: {
    NEXT_PUBLIC_STATE: z.enum(['dc', 'co']),
    NEXT_PUBLIC_GA_ID: z.string().startsWith('G-').optional(),
    NEXT_PUBLIC_SOCURE_SDK_KEY: z.string().min(1).optional(),
    NEXT_PUBLIC_SOCURE_DI_SDK_KEY: z.string().min(1).optional(),
    NEXT_PUBLIC_MOCK_SOCURE: z.enum(['true', 'false']).optional(),
    /**
     * Development only: when `true`, IalGuard still sends users to OIDC step-up even if the portal JWT already has IAL1+.
     * No effect unless NODE_ENV is `development`.
     */
    NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP: z.enum(['true', 'false']).optional()
  },

  /**
   * Runtime environment variables
   * Map to process.env
   */
  runtimeEnv: {
    NODE_ENV: process.env.NODE_ENV,
    BACKEND_URL: process.env.BACKEND_URL,
    ENROLLMENT_CHECKER_ORIGIN: process.env.ENROLLMENT_CHECKER_ORIGIN,
    NEXT_PUBLIC_STATE: process.env.NEXT_PUBLIC_STATE,
    NEXT_PUBLIC_GA_ID: process.env.NEXT_PUBLIC_GA_ID,
    NEXT_PUBLIC_SOCURE_SDK_KEY: process.env.NEXT_PUBLIC_SOCURE_SDK_KEY,
    NEXT_PUBLIC_SOCURE_DI_SDK_KEY: process.env.NEXT_PUBLIC_SOCURE_DI_SDK_KEY,
    NEXT_PUBLIC_MOCK_SOCURE: process.env.NEXT_PUBLIC_MOCK_SOCURE,
    NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP: process.env.NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP
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
