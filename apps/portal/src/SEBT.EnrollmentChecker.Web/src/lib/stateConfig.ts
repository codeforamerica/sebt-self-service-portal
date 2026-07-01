import { env } from './env'

export interface EnrollmentStateConfig {
  state: 'dc' | 'co'
  showSchoolField: boolean
  checkerEnabled: boolean
  botProtectionEnabled: boolean
  portalUrl: string
  applicationUrl: string
  /** SSG: portal Node server URL. SSR: '' (same-origin /api routes). */
  apiBaseUrl: string
}

export function getEnrollmentConfig(): EnrollmentStateConfig {
  return {
    state: env.NEXT_PUBLIC_STATE,
    showSchoolField: env.NEXT_PUBLIC_SHOW_SCHOOL_FIELD,
    checkerEnabled: env.NEXT_PUBLIC_CHECKER_ENABLED,
    botProtectionEnabled: env.NEXT_PUBLIC_BOT_PROTECTION_ENABLED,
    portalUrl: env.NEXT_PUBLIC_PORTAL_URL,
    applicationUrl: env.NEXT_PUBLIC_APPLICATION_URL,
    apiBaseUrl: env.NEXT_PUBLIC_API_BASE_URL ?? ''
  }
}
