'use client'

import { languageNames, type SupportedLanguage } from '@/lib/i18n'
import { getStateConfig } from '@/lib/state'
import Image from 'next/image'
import { useEffect, useRef, useState } from 'react'

import type { MobileLanguageSelectorProps } from './types'

/** Mobile language selector - accordion dropdown */
export function MobileLanguageSelector({
  languages,
  languageCodes,
  currentLang,
  onLanguageSelect,
  t,
  state
}: MobileLanguageSelectorProps) {
  const config = getStateConfig(state)
  const [isOpen, setIsOpen] = useState(false)
  const menuRef = useRef<HTMLUListElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

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
      return () => document.removeEventListener('mousedown', handleClickOutside)
    }
    return undefined
  }, [isOpen])

  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === 'Escape') {
      setIsOpen(false)
      buttonRef.current?.focus()
    }
  }

  const handleSelect = (lang: SupportedLanguage) => {
    onLanguageSelect(lang)
    setIsOpen(false)
    buttonRef.current?.focus()
  }

  return (
    <div className="usa-language__mobile">
      <ul className="usa-language__primary usa-accordion">
        <li className="usa-language__primary-item">
          <button
            ref={buttonRef}
            type="button"
            className={`usa-button usa-language__link${config.languageSelectorClass ? ` ${config.languageSelectorClass}` : ''}`}
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
              {languageCodes.map((code, index) => (
                <span key={code}>
                  {index > 0 && <span>,&nbsp;</span>}
                  {/* eslint-disable-next-line security/detect-object-injection -- code is typed SupportedLanguage */}
                  <span lang={code}>{languageNames[code]}</span>
                </span>
              ))}
            </div>
          </button>
          <ul
            ref={menuRef}
            id="language-options"
            className={`usa-language__submenu${config.languageSubmenuClass ? ` ${config.languageSubmenuClass}` : ''}`}
            hidden={!isOpen}
            aria-hidden={!isOpen}
            role="menu"
          >
            {languages.map((lang) => (
              <li
                key={lang.code}
                className="usa-language__submenu-item"
                role="none"
              >
                <button
                  type="button"
                  onClick={() => handleSelect(lang.code)}
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
  )
}
