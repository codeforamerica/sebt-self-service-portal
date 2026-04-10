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
    BACKEND_URL: z.url().default('http://localhost:5280')
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
    /**
     * Development only: when `true`, IalGuard still sends users to OIDC step-up even if the portal JWT already has IAL1+.
     * No effect unless NODE_ENV is `development`.
     */
    NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP: z.enum(['true', 'false']).optional(),
    /**
     * Max age (years) for portal JWT `id_proofing_completed_at` before IalGuard sends the user through OIDC step-up again.
     * When `id_proofing_expires_at` is on the JWT, it is enforced as well (must be before that instant).
     * Default 5 when unset (see parseIdProofingMaxAgeYears).
     */
    NEXT_PUBLIC_CO_ID_PROOFING_MAX_AGE_YEARS: z.string().optional()
  },

  /**
   * Runtime environment variables
   * Map to process.env
   */
  runtimeEnv: {
    NODE_ENV: process.env.NODE_ENV,
    BACKEND_URL: process.env.BACKEND_URL,
    NEXT_PUBLIC_STATE: process.env.NEXT_PUBLIC_STATE,
    NEXT_PUBLIC_GA_ID: process.env.NEXT_PUBLIC_GA_ID,
    NEXT_PUBLIC_SOCURE_SDK_KEY: process.env.NEXT_PUBLIC_SOCURE_SDK_KEY,
    NEXT_PUBLIC_SOCURE_DI_SDK_KEY: process.env.NEXT_PUBLIC_SOCURE_DI_SDK_KEY,
    NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP: process.env.NEXT_PUBLIC_DEBUG_REPEAT_OIDC_STEP_UP,
    NEXT_PUBLIC_CO_ID_PROOFING_MAX_AGE_YEARS: process.env.NEXT_PUBLIC_CO_ID_PROOFING_MAX_AGE_YEARS
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
