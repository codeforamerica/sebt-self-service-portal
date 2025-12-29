'use client'

import type { LanguageSelectorSubProps } from './types'

/** Desktop language selector - horizontal button list */
export function DesktopLanguageSelector({
  languages,
  currentLang,
  onLanguageSelect,
  t
}: LanguageSelectorSubProps) {
  return (
    <nav
      className="usa-language__desktop"
      aria-label={t('languageSelector')}
    >
      <ul className="usa-language__list">
        {languages.map((lang) => (
          <li
            key={lang.code}
            className="usa-language__list-item"
          >
            <button
              type="button"
              onClick={() => onLanguageSelect(lang.code)}
              lang={lang.code}
              aria-current={currentLang === lang.code ? 'true' : undefined}
              className="usa-language__link-button"
            >
              {t(lang.key)}
            </button>
          </li>
        ))}
      </ul>
    </nav>
  )
}
