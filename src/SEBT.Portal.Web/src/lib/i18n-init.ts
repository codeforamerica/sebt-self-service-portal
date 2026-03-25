import { initI18n } from '@sebt/design-system/client'
import { namespaces, stateResources } from './generated-locale-resources'

const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase()
initI18n(stateResources, namespaces, state)
