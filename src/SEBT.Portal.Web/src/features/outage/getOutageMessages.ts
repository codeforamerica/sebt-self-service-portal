import { stateResources } from '@/lib/generated-locale-resources'
import { getState, getStateConfig, type SupportedLanguage } from '@sebt/design-system'

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

type StateLanguageBundles = Record<string, { outage?: OutageCopy }>

function getOutageNamespace(language: SupportedLanguage, state: ReturnType<typeof getState>) {
  const bundles = stateResources[state] as StateLanguageBundles | undefined
  return bundles?.[language]?.outage
}

export function getOutageMessages(): OutageMessage[] {
  const state = getState()
  const languages = getStateConfig(state).supportedLanguages

  return languages.flatMap((language) => {
    const copy = getOutageNamespace(language, state)
    const body1 = copy?.body1?.trim() ?? ''
    const body2 = copy?.body2?.trim() ?? ''

    if (!body1 && !body2) {
      return []
    }

    return [{ language, body1, body2 }]
  })
}

export function getOutageFooterCopy(): OutageFooterCopy[] {
  const state = getState()
  const copyEn = getOutageNamespace('en', state)
  const copyEs = getOutageNamespace('es', state)

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
