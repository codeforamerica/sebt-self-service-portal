import { initI18n } from '@sebt/design-system/client'
import { CHECKER_DEFAULT_STATE } from './checkerState'
import { namespaces, stateResources } from './generated-locale-resources'

// next.config.ts always publishes NEXT_PUBLIC_STATE (derived from STATE), so
// the fallback only applies outside a Next build — notably vitest, which pins
// it in vitest.config.ts. Shares one constant with next.config.ts so the two
// cannot disagree about which state an unconfigured build means.
const state = (process.env.NEXT_PUBLIC_STATE || CHECKER_DEFAULT_STATE).toLowerCase()
initI18n(stateResources as Parameters<typeof initI18n>[0], namespaces, state)
