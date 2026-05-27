import { createEnv } from '@t3-oss/env-nextjs'
import { z } from 'zod'

// z.coerce.boolean() uses Boolean() which treats any non-empty string as true,
// so "false" would coerce to true. This handles the common env var string values.
const boolEnv = (defaultValue = false) =>
  z.preprocess(
    (val) => (val === 'false' || val === '0' ? false : Boolean(val)),
    z.boolean().optional().default(defaultValue)
  )

export const env = createEnv({
  server: {
    NODE_ENV: z.enum(['development', 'test', 'production']).optional(),
    BACKEND_URL: z.string().url().default('http://localhost:5280')
  },
  client: {
    NEXT_PUBLIC_STATE: z.enum(['dc', 'co']),
    NEXT_PUBLIC_BASE_PATH: z.string().optional().default(''),
    NEXT_PUBLIC_API_BASE_URL: z.string().url().optional(),
    NEXT_PUBLIC_SHOW_SCHOOL_FIELD: boolEnv(false),
    NEXT_PUBLIC_CHECKER_ENABLED: boolEnv(true),
    NEXT_PUBLIC_BOT_PROTECTION_ENABLED: boolEnv(false),
    NEXT_PUBLIC_PORTAL_URL: z.string().url(),
    NEXT_PUBLIC_APPLICATION_URL: z.string().url(),
    NEXT_PUBLIC_GA_ID: z.string().regex(/^G-/).optional()
  },
  runtimeEnv: {
    NODE_ENV: process.env.NODE_ENV,
    BACKEND_URL: process.env.BACKEND_URL,
    NEXT_PUBLIC_STATE: process.env.NEXT_PUBLIC_STATE,
    NEXT_PUBLIC_BASE_PATH: process.env.NEXT_PUBLIC_BASE_PATH,
    NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL,
    NEXT_PUBLIC_SHOW_SCHOOL_FIELD: process.env.NEXT_PUBLIC_SHOW_SCHOOL_FIELD,
    NEXT_PUBLIC_CHECKER_ENABLED: process.env.NEXT_PUBLIC_CHECKER_ENABLED,
    NEXT_PUBLIC_BOT_PROTECTION_ENABLED: process.env.NEXT_PUBLIC_BOT_PROTECTION_ENABLED,
    NEXT_PUBLIC_PORTAL_URL: process.env.NEXT_PUBLIC_PORTAL_URL,
    NEXT_PUBLIC_APPLICATION_URL: process.env.NEXT_PUBLIC_APPLICATION_URL,
    NEXT_PUBLIC_GA_ID: process.env.NEXT_PUBLIC_GA_ID
  },
  skipValidation: !!process.env.SKIP_ENV_VALIDATION,
  emptyStringAsUndefined: true
})
