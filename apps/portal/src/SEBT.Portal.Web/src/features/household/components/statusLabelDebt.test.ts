import { type StateCode } from '@sebt/design-system'
import { describe, expect, it } from 'vitest'

import { stateResources } from '@/lib/generated-locale-resources'
import { PROVISIONAL_EN } from '@/lib/provisionalContent'

import { APPLICATION_STATUS_KEYS } from './ApplicationsSection/ApplicationsSection'
import { PROFILE_STATUS_LABEL_KEYS } from './HouseholdSummary/HouseholdSummary'

/**
 * Keeps the provisional English bundle honest.
 *
 * PROVISIONAL_EN exists only because six content rows are not yet in the source spreadsheet. It
 * is hardcoded copy living in the app, which the localization rules forbid as a permanent state.
 * These tests are the forcing function: they fail when an entry is no longer needed, and they
 * fail if someone injects a key that is not a dashboard status label.
 *
 * When one of these fails because a row landed, delete that entry from PROVISIONAL_EN. Do not
 * relax the test.
 */

const STATES: readonly StateCode[] = ['dc', 'co']

const STATUS_LABEL_KEYS = [...Object.values(APPLICATION_STATUS_KEYS), ...PROFILE_STATUS_LABEL_KEYS]

const PROVISIONAL_DASHBOARD_KEYS = Object.keys(PROVISIONAL_EN.dashboard)

function authoredEnglishKeys(state: StateCode): string[] {
  // eslint-disable-next-line security/detect-object-injection -- state comes from the STATES literal
  const bundles = stateResources[state] as Record<string, Record<string, object>> | undefined
  return Object.keys(bundles?.en?.dashboard ?? {})
}

describe('provisional content debt', () => {
  it('only covers the dashboard namespace', () => {
    // A wider scope invites injecting a key whose absence is a deliberate feature gate, such as
    // applicationsTableHeadingNumber, which ApplicationsSection reads through i18n.exists().
    expect(Object.keys(PROVISIONAL_EN)).toEqual(['dashboard'])
  })

  it.each(PROVISIONAL_DASHBOARD_KEYS)(
    '%s is a dashboard status label, not an arbitrary key',
    (key) => {
      expect(
        STATUS_LABEL_KEYS,
        `"${key}" is not a status label. Provisional content is an allowlist: a key whose absence ` +
          `is meaningful, such as applicationsTableHeadingNumber, must never be injected.`
      ).toContain(key)
    }
  )

  it.each(PROVISIONAL_DASHBOARD_KEYS)('%s is still unauthored in at least one state', (key) => {
    const statesMissingKey = STATES.filter((state) => !authoredEnglishKeys(state).includes(key))

    expect(
      statesMissingKey,
      `Every state now authors "${key}". Delete it from PROVISIONAL_EN in src/lib/provisionalContent.ts.`
    ).not.toHaveLength(0)
  })
})
