// StateResources is imported type-only: lib/i18n initializes react-i18next at module
// scope, and pulling that runtime here would break the server-safe main barrel.
import type { StateResources } from '../../lib/i18n'
import { getState, getStateConfig, type SupportedLanguage } from '../../lib/state'

export interface OutageMessage {
  language: SupportedLanguage
  body1: string
  body2: string
}

export interface OutageFooterCopy {
  language: SupportedLanguage
  prefix: string
}

type OutageCopy = {
  body1?: string
  body2?: string
  footer?: string
}

function getOutageNamespace(
  resources: StateResources,
  language: SupportedLanguage,
  state: ReturnType<typeof getState>
): OutageCopy | undefined {
  return resources[state]?.[language]?.['outage']
}

/**
 * Outage copy for each of the current state's supported languages, read from the app's
 * generated locale resources. Languages with no outage copy are omitted, so the page
 * only stacks sections that actually have content.
 */
export function getOutageMessages(resources: StateResources): OutageMessage[] {
  const state = getState()
  const languages = getStateConfig(state).supportedLanguages

  return languages.flatMap((language) => {
    const copy = getOutageNamespace(resources, language, state)
    const body1 = copy?.body1?.trim() ?? ''
    const body2 = copy?.body2?.trim() ?? ''

    if (!body1 && !body2) {
      return []
    }

    return [{ language, body1, body2 }]
  })
}

export function getOutageFooterCopy(resources: StateResources): OutageFooterCopy[] {
  const state = getState()
  const copyEn = getOutageNamespace(resources, 'en', state)
  const copyEs = getOutageNamespace(resources, 'es', state)

  return [
    {
      language: 'en',
      prefix: copyEn?.footer?.trim() ?? 'For more information, visit'
    },
    {
      language: 'es',
      prefix: copyEs?.footer?.trim() ?? 'Para más información, visite'
    }
  ]
}
