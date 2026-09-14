import { i18n, initI18n } from '@sebt/design-system/client'
// The state module directly, not the package index: every test loads this file from
// setup, and the index would pull in components that bind next/navigation before a
// test's vi.mock of it can apply.
import { getState } from '@sebt/design-system/src/lib/state'

import { namespaces, stateResources } from './generated-locale-resources'
import { PROVISIONAL_EN } from './provisionalContent'

// getState() rather than process.env: STATE is server-only and NEXT_PUBLIC_STATE is
// not inlined into this bundle, so in the browser both read as undefined and every
// state would silently load DC's copy. getState() reads the <html data-state> the
// server stamps per request, and falls back to STATE when rendering on the server.
initI18n(stateResources, namespaces, getState())

// Deep merge, never overwrite: authored content always beats the provisional English.
for (const [namespace, resources] of Object.entries(PROVISIONAL_EN)) {
  i18n.addResourceBundle('en', namespace, resources, true, false)
}
