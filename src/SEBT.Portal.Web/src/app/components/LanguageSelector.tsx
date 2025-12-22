'use client'

import { changeLanguage, type SupportedLanguage } from '@/src/lib/i18n'
import Image from 'next/image'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * LanguageSelector Component
 *
 * Responsive language switcher following USWDS patterns:
 * - Desktop: Inline text links with underline on hover
 * - Mobile: Dropdown menu with translate button
 *
 * @see https://designsystem.digital.gov/components/language-selector/
 */

// Language codes with their translation keys from common.json
const LANGUAGES = [
  { code: 'en' as SupportedLanguage, key: 'english' },
  { code: 'es' as SupportedLanguage, key: 'español' },
  { code: 'am' as SupportedLanguage, key: 'amharic' }
] as const

interface LanguageSelectorProps {
  state?: string
}

export function LanguageSelector({ state = 'dc' }: LanguageSelectorProps) {
  const { t, i18n } = useTranslation('common')
  const [isOpen, setIsOpen] = useState(false)
  const menuRef = useRef<HTMLUListElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  const currentLang = (i18n.language || 'en') as SupportedLanguage

  // Close menu when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        menuRef.current &&
        buttonRef.current &&
        !menuRef.current.contains(event.target as Node) &&
        !buttonRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false)
      }
    }

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside)
    }
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [isOpen])

  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === 'Escape') {
      setIsOpen(false)
      buttonRef.current?.focus()
    }
  }

  const handleLanguageSelect = (lang: SupportedLanguage) => {
    changeLanguage(lang)
    setIsOpen(false)
    buttonRef.current?.focus()
  }

  return (
    <div className="usa-language-container">
      {/* Desktop: Inline links */}
      <nav
        className="usa-language__desktop"
        aria-label={t('languageSelector')}
      >
        <ul className="usa-language__list">
          {LANGUAGES.map((lang) => (
            <li
              key={lang.code}
              className="usa-language__list-item"
            >
              <button
                type="button"
                onClick={() => handleLanguageSelect(lang.code)}
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

      {/* Mobile: Dropdown menu */}
      <div className="usa-language__mobile">
        <ul className="usa-language__primary usa-accordion">
          <li className="usa-language__primary-item">
            <button
              ref={buttonRef}
              type="button"
              className="usa-button usa-language__link"
              aria-expanded={isOpen}
              aria-controls="language-options"
              onClick={() => setIsOpen(!isOpen)}
              onKeyDown={handleKeyDown}
            >
              <div>
                <Image
                  src={`/images/states/${state}/icons/translate_Rounded.svg`}
                  alt=""
                  width={16}
                  height={16}
                  aria-hidden="true"
                  className="margin-right-1"
                />
                <span>{t('translate')}</span>
              </div>
              <div>
                <span lang="en">English</span>
                <span>,&nbsp;</span>
                <span lang="es">Español</span>
                <span>,&nbsp;</span>
                <span lang="am">አማርኛ</span>
              </div>
            </button>
            <ul
              ref={menuRef}
              id="language-options"
              className="usa-language__submenu"
              hidden={!isOpen}
              role="menu"
            >
              {LANGUAGES.map((lang) => (
                <li
                  key={lang.code}
                  className="usa-language__submenu-item"
                  role="none"
                >
                  <button
                    type="button"
                    onClick={() => handleLanguageSelect(lang.code)}
                    onKeyDown={handleKeyDown}
                    lang={lang.code}
                    role="menuitem"
                    aria-current={currentLang === lang.code ? 'true' : undefined}
                    className="usa-language__submenu-button"
                  >
                    {t(lang.key)}
                  </button>
                </li>
              ))}
            </ul>
          </li>
        </ul>
      </div>
    </div>
  )
}
