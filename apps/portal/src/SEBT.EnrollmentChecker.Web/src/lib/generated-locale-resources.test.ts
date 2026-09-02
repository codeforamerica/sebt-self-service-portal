import { describe, expect, it } from 'vitest'

import { namespaces, stateResources } from './generated-locale-resources'

// Guards the copy:generate --sections allowlist: S11 rows must survive generation
// for this app. If the maintenance namespace disappears from the generated bundle,
// the maintenance page silently renders raw keys.
describe('generated locale resources', () => {
  it('registers the maintenance namespace', () => {
    expect(namespaces).toContain('maintenanceEnrollmentChecker')
  })

  it.each(['co', 'dc'] as const)('carries %s maintenance copy', (state) => {
    const bundle = stateResources[state].en as Record<string, Record<string, string> | undefined>
    expect(bundle['maintenanceEnrollmentChecker']?.title).toBeTruthy()
  })
})
