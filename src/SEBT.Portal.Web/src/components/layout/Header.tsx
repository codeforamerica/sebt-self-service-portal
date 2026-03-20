'use client'

import { supportedLanguages } from '@/lib/i18n'
import Image from 'next/image'
import Link from 'next/link'
import { useTranslation } from 'react-i18next'
import { LanguageSelector } from './LanguageSelector'
import type { HeaderProps } from './types'

// Logo dimensions match each state's SVG viewBox so the image renders
// at its natural aspect ratio. maxh-6 caps the height for states with
// taller logos (DC), while wider logos (CO) spread horizontally.
const logoDimensions: Record<string, { width: number; height: number }> = {
  dc: { width: 122, height: 52 },
  co: { width: 192, height: 28 }
}

export function Header({ state = 'dc' }: HeaderProps) {
  const { t } = useTranslation('common')
  const defaultDimensions = { width: 122, height: 52 }
  // eslint-disable-next-line security/detect-object-injection -- state is typed StateCode
  const { width, height } = logoDimensions[state] ?? defaultDimensions

  return (
    <header
      className="usa-header usa-header--basic bg-white shadow-2"
      role="banner"
    >
      <div className="display-flex flex-justify flex-align-center width-full padding-y-105 padding-x-2">
        <div className="usa-navbar border-0">
          <div className="usa-logo margin-left-0">
            <Link
              href="/"
              title="Home"
              aria-label="Home"
              className="display-flex flex-align-center"
            >
              <Image
                src={`/images/states/${state}/logo.svg`}
                alt={t('logoAlt')}
                width={width}
                height={height}
                priority
                className="maxw-full height-auto maxh-6"
              />
            </Link>
          </div>
        </div>

        <LanguageSelector
          state={state}
          languages={supportedLanguages}
        />
      </div>
    </header>
  )
}
