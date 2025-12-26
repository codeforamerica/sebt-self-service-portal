'use client'

import { changeLanguage, supportedLanguages, type SupportedLanguage } from '@/lib/i18n'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'

import type { LanguageSelectorProps } from '../types'
import { languageTranslationKeys } from './constants'
import { DesktopLanguageSelector } from './DesktopLanguageSelector'
import { MobileLanguageSelector } from './MobileLanguageSelector'

/** Main language selector component - renders both desktop and mobile versions */
export function LanguageSelector({
  state = 'dc',
  languages = supportedLanguages
}: LanguageSelectorProps) {
  const { t, i18n } = useTranslation('common')
  const currentLang = (i18n.language || 'en') as SupportedLanguage

  // Derive LANGUAGES from the prop, memoized for performance
  const LANGUAGES = useMemo(
    () =>
      languages.map((code: SupportedLanguage) => ({
        code,
        // eslint-disable-next-line security/detect-object-injection -- code is typed SupportedLanguage, not user input
        key: languageTranslationKeys[code]
      })),
    [languages]
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
        languageCodes={languages}
        currentLang={currentLang}
        onLanguageSelect={handleLanguageSelect}
        t={t}
        state={state}
      />
    </div>
  )
}
