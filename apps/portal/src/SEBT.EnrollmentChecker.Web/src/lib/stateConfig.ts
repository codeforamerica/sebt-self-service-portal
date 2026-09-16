import { getClientConfig } from './client-config'
import { env } from './env'

export interface EnrollmentStateConfig {
  state: 'dc' | 'co'
  showSchoolField: boolean
  checkerEnabled: boolean
  botProtectionEnabled: boolean
  /**
   * Absent only on a misconfigured deployment: config.js supplies it in every
   * environment. Callers hide the portal CTA rather than render a dead link.
   */
  portalUrl: string | undefined
  /** Absent when no application destination is configured (applications closed, DC-701). */
  applicationUrl: string | undefined
  /** SSG: portal Node server URL. SSR: '' (same-origin /api routes). */
  apiBaseUrl: string
}

export function getEnrollmentConfig(): EnrollmentStateConfig {
  // state stays build-time (it selects per-state assets); everything else is
  // resolved at runtime so one artifact serves every environment.
  const config = getClientConfig()
  return {
    state: env.NEXT_PUBLIC_STATE,
    showSchoolField: config.showSchoolField,
    checkerEnabled: config.checkerEnabled,
    botProtectionEnabled: config.botProtectionEnabled,
    portalUrl: config.portalUrl,
    applicationUrl: config.applicationUrl,
    apiBaseUrl: config.apiBaseUrl ?? ''
  }
}
