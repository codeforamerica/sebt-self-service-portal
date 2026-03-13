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
    // OIDC vars are required for CO (MyColorado login) but unused by DC.
    // Marked optional here because createEnv cannot cross-reference NEXT_PUBLIC_STATE.
    // Runtime validation in /api/auth/oidc/callback/route.ts returns 503 if missing.
    OIDC_DISCOVERY_ENDPOINT: z.string().url().optional(),
    OIDC_CLIENT_ID: z.string().optional(),
    OIDC_CLIENT_SECRET: z.string().optional(),
    OIDC_REDIRECT_URI: z.string().url().optional(),
    OIDC_LANGUAGE_PARAM: z.string().optional(),
    OIDC_COMPLETE_LOGIN_SIGNING_KEY: z.string().min(32).optional()
  },

  /**
   * Client-side environment variables
   * Must be prefixed with NEXT_PUBLIC_
   */
  client: {
    NEXT_PUBLIC_STATE: z.enum(['dc', 'co']),
    NEXT_PUBLIC_GA_ID: z.string().startsWith('G-').optional(),
    NEXT_PUBLIC_SOCURE_SDK_KEY: z.string().min(1).optional()
  },

  /**
   * Runtime environment variables
   * Map to process.env
   */
  runtimeEnv: {
    NODE_ENV: process.env.NODE_ENV,
    BACKEND_URL: process.env.BACKEND_URL,
    OIDC_DISCOVERY_ENDPOINT: process.env.OIDC_DISCOVERY_ENDPOINT,
    OIDC_CLIENT_ID: process.env.OIDC_CLIENT_ID,
    OIDC_CLIENT_SECRET: process.env.OIDC_CLIENT_SECRET,
    OIDC_REDIRECT_URI: process.env.OIDC_REDIRECT_URI,
    OIDC_LANGUAGE_PARAM: process.env.OIDC_LANGUAGE_PARAM,
    OIDC_COMPLETE_LOGIN_SIGNING_KEY: process.env.OIDC_COMPLETE_LOGIN_SIGNING_KEY,
    NEXT_PUBLIC_STATE: process.env.NEXT_PUBLIC_STATE,
    NEXT_PUBLIC_GA_ID: process.env.NEXT_PUBLIC_GA_ID,
    NEXT_PUBLIC_SOCURE_SDK_KEY: process.env.NEXT_PUBLIC_SOCURE_SDK_KEY
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
