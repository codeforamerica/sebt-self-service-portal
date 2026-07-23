import { describe, expect, it } from 'vitest'

import { deriveEnvironmentFromHost } from './environment'

describe('deriveEnvironmentFromHost', () => {
  it('returns the environment prefix for non-production hosts', () => {
    expect(deriveEnvironmentFromHost('dev.co.sebt-portal.codeforamerica.app')).toBe('dev')
    expect(deriveEnvironmentFromHost('dev.co.sebt-enrollment.codeforamerica.app')).toBe('dev')
    expect(deriveEnvironmentFromHost('dev.dc.sebt-portal.codeforamerica.app')).toBe('dev')
    expect(deriveEnvironmentFromHost('staging.co.sebt-portal.codeforamerica.app')).toBe('staging')
    expect(deriveEnvironmentFromHost('test.co.sebt-portal.codeforamerica.app')).toBe('test')
    expect(deriveEnvironmentFromHost('qa.co.sebt-portal.codeforamerica.app')).toBe('qa')
    expect(deriveEnvironmentFromHost('uat.co.sebt-portal.codeforamerica.app')).toBe('uat')
  })

  it('returns "local" for local development hosts', () => {
    expect(deriveEnvironmentFromHost('localhost')).toBe('local')
    expect(deriveEnvironmentFromHost('127.0.0.1')).toBe('local')
    expect(deriveEnvironmentFromHost('sebt.local')).toBe('local')
  })

  it('treats production hosts (no environment prefix) as "production"', () => {
    expect(deriveEnvironmentFromHost('co.sebt-portal.codeforamerica.app')).toBe('production')
    expect(deriveEnvironmentFromHost('dc.sebt-portal.codeforamerica.app')).toBe('production')
    expect(deriveEnvironmentFromHost('sunbucks.dc.gov')).toBe('production')
    expect(deriveEnvironmentFromHost('benefits.sunbucks.dc.gov')).toBe('production')
  })

  it('defaults unknown or empty hosts to "production"', () => {
    expect(deriveEnvironmentFromHost('')).toBe('production')
    expect(deriveEnvironmentFromHost('example.com')).toBe('production')
  })
})
