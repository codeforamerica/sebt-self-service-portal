import {
  getState,
  getStateConfig,
  type StateCode,
  type SupportedLanguage
} from '@sebt/design-system/src/lib/state'

// Per-state structure of the landing page. Declared here rather than branched on
// inside the component, so adding a state is a data change.

export interface LandingConfig {
  /**
   * Whether the eligibility explanation sits behind a USWDS accordion.
   *
   * States that set this false render the same copy inline. They also have no
   * `accordionTitle` in their content, so enabling it would render a raw key.
   */
  useAccordion: boolean
}

const landingConfigs: Record<StateCode, LandingConfig> = {
  dc: { useAccordion: false },
  co: { useAccordion: true }
}

/**
 * Which copy set the landing page renders. `closed` is the post-season page at
 * /closed, where the check still works but the framing changes from "should I
 * apply?" to "was my student enrolled?".
 */
export type LandingVariant = 'open' | 'closed'

/**
 * i18next key holding each language's start-check button label. Mapped
 * explicitly because the content keys aren't uniform (`action`, not
 * `actionEnglish`).
 */
const actionKeyByLanguage: Record<LandingVariant, Record<SupportedLanguage, string>> = {
  open: {
    en: 'action',
    es: 'actionEspañol',
    am: 'actionAmharic'
  },
  closed: {
    en: 'closedAction',
    es: 'closedActionEspañol',
    am: 'closedActionAmharic'
  }
}

/** Analytics identifier per language. Existing dashboards filter on these — don't rename. */
const analyticsCtaByLanguage: Record<SupportedLanguage, string> = {
  en: 'start_enrollment_check_cta',
  es: 'start_enrollment_check_cta_es',
  am: 'start_enrollment_check_cta_am'
}

export interface LandingAction {
  language: SupportedLanguage
  /** Key within the `landing` namespace holding this button's label. */
  translationKey: string
  analyticsCta: string
  /** `undefined` renders the design system's default (filled) button. */
  variant?: 'outline'
}

/** Landing config for the active state. */
export function getLandingConfig(): LandingConfig {
  const state = getState()
  // eslint-disable-next-line security/detect-object-injection -- state is typed StateCode
  return landingConfigs[state] ?? landingConfigs.dc
}

/**
 * One start-check button per language the state supports, in its listed order.
 * The first is filled and the rest outline, so the hierarchy holds at any count.
 *
 * Both variants share analytics identifiers — the button starts the same check
 * either way, and splitting them would fragment existing dashboards.
 */
export function getLandingActions(variant: LandingVariant = 'open'): LandingAction[] {
  const { supportedLanguages } = getStateConfig(getState())
  // eslint-disable-next-line security/detect-object-injection -- variant is typed LandingVariant
  const keys = actionKeyByLanguage[variant]

  return supportedLanguages.map((language, index) => ({
    language,
    // eslint-disable-next-line security/detect-object-injection -- language is typed SupportedLanguage
    translationKey: keys[language],
    // eslint-disable-next-line security/detect-object-injection -- language is typed SupportedLanguage
    analyticsCta: analyticsCtaByLanguage[language],
    ...(index > 0 && { variant: 'outline' as const })
  }))
}
