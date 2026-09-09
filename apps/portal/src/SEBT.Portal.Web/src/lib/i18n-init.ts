import { i18n, initI18n } from '@sebt/design-system/client'

import { namespaces, stateResources } from './generated-locale-resources'
import { PROVISIONAL_EN } from './provisionalContent'

const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase()
initI18n(stateResources, namespaces, state)

// Deep merge, never overwrite: authored content always beats the provisional English.
for (const [namespace, resources] of Object.entries(PROVISIONAL_EN)) {
  i18n.addResourceBundle('en', namespace, resources, true, false)
}
