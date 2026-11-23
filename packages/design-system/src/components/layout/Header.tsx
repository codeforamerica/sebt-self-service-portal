'use client'

import { supportedLanguages } from '../../lib/i18n'
import { getStateConfig } from '../../lib/state'
import Image from 'next/image'
import { useTranslation } from 'react-i18next'
import { LanguageSelector } from './LanguageSelector'
import type { HeaderProps } from './types'

// maxh-6 caps the height for taller logos (DC); width-auto on the image lets the
// width follow, so capping scales rather than squashes.
export function Header({ state }: HeaderProps) {
  const { t } = useTranslation('common')
  const { logo } = getStateConfig(state).assets

  return (
    <header
      className="usa-header usa-header--basic bg-white shadow-2"
      role="banner"
    >
      <div className="display-flex flex-justify flex-align-center width-full padding-y-105 padding-x-2">
        <div className="usa-navbar border-0">
          <div className="usa-logo margin-left-0">
            <Image
              src={logo.src}
              alt={t('bannerImageAltText')}
              width={logo.width}
              height={logo.height}
              priority
              className="maxw-full width-auto height-auto maxh-6"
            />
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
