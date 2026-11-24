import { describe, expect, it } from 'vitest'

import { STATE_CODES } from '@sebt/design-system/src/lib/state'

import { namespaces, stateResources } from './generated-locale-resources'

// Guards the copy:generate --sections allowlist: S11 rows must survive generation
// for this app. If the maintenance namespace disappears from the generated bundle,
// the maintenance page silently renders raw keys.
describe('generated locale resources', () => {
  it('registers the maintenance namespace', () => {
    expect(namespaces).toContain('maintenanceEnrollmentChecker')
  })

  it.each(STATE_CODES)('carries %s maintenance copy', (state) => {
    const bundle = stateResources[state].en as Record<string, Record<string, string> | undefined>
    expect(bundle['maintenanceEnrollmentChecker']?.title).toBeTruthy()
  })
})
