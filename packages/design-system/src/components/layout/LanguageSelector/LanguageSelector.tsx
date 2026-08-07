'use client'

import { changeLanguage, type SupportedLanguage } from '../../../lib/i18n'
import { getStateConfig } from '../../../lib/state'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'

import type { LanguageSelectorProps } from '../types'
import { languageTranslationKeys } from './constants'
import { DesktopLanguageSelector } from './DesktopLanguageSelector'
import { MobileLanguageSelector } from './MobileLanguageSelector'

/** Main language selector component - renders both desktop and mobile versions */
export function LanguageSelector({ state, languages }: LanguageSelectorProps) {
  const { t, i18n } = useTranslation('common')
  const currentLang = (i18n.language || 'en') as SupportedLanguage
  const resolvedState = state ?? 'dc'

  // Languages follow the state being rendered. Resolving them from a deployment-wide
  // value lets the list disagree with the rest of the component, which surfaces as a
  // language option the state has no content for: an empty but focusable control that
  // screen readers still announce. `getStateConfig` returns the same array instance on
  // every call, so this stays referentially stable for the memo below.
  const resolvedLanguages = languages ?? getStateConfig(resolvedState).supportedLanguages

  // Derive LANGUAGES from the resolved list, memoized for performance
  const LANGUAGES = useMemo(
    () =>
      resolvedLanguages.map((code: SupportedLanguage) => ({
        code,
        // eslint-disable-next-line security/detect-object-injection -- code is typed SupportedLanguage, not user input
        key: languageTranslationKeys[code]
      })),
    [resolvedLanguages]
  )

  const handleLanguageSelect = (lang: SupportedLanguage) => {
    changeLanguage(lang)
  }

  return (
    <div className="usa-language-container">
      <DesktopLanguageSelector
        languages={LANGUAGES}
        currentLang={currentLang}
        onLanguageSelect={handleLanguageSelect}
        t={t}
      />
      <MobileLanguageSelector
        languages={LANGUAGES}
        languageCodes={resolvedLanguages}
        currentLang={currentLang}
        onLanguageSelect={handleLanguageSelect}
        t={t}
        state={resolvedState}
      />
    </div>
  )
}
