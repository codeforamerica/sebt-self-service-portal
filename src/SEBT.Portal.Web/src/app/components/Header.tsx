'use client'

import type { HeaderProps } from '@/src/types/components'
import Image from 'next/image'
import Link from 'next/link'
import { useTranslation } from 'react-i18next'
import { LanguageSelector } from './LanguageSelector'

export function Header({ state = 'dc' }: HeaderProps) {
  const { t } = useTranslation('common')

  return (
    <header
      className="usa-header usa-header--basic bg-white shadow-2"
      role="banner"
    >
      <div className="display-flex flex-justify flex-align-center width-full padding-y-3 padding-left-1 padding-right-3">
        <div className="usa-navbar border-0">
          <div className="usa-logo">
            <Link
              href="/"
              title="Home"
              aria-label="Home"
              className="display-flex flex-align-center"
            >
              <Image
                src={`/images/states/${state}/logo.svg`}
                alt={t('logoAlt')}
                width={121}
                height={51}
                priority
              />
            </Link>
          </div>
        </div>

        <LanguageSelector state={state} />
      </div>
    </header>
  )
}
