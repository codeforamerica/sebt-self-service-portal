'use client'

import type { HelpSectionProps } from '@/src/types/components'
import Image from 'next/image'
import Link from 'next/link'
import { useTranslation } from 'react-i18next'

export function HelpSection({ state = 'dc' }: HelpSectionProps) {
  const { t } = useTranslation('common')

  return (
    <section
      className="bg-secondary padding-x-3"
      aria-labelledby="help-section-title"
    >
      <h2
        id="help-section-title"
        className="usa-sr-only"
      >
        Help and Support
      </h2>

      <div className="display-flex flex-column flex-align-center maxw-tablet margin-x-auto">
        <Link
          href="/faqs"
          className="display-flex flex-column flex-align-center text-no-underline text-ink padding-top-2 radius-lg hover:opacity-80"
        >
          <div>
            <Image
              src={`/images/states/${state}/icons/faqs-icon.svg`}
              alt=""
              width={109}
              height={109}
              aria-hidden="true"
            />
          </div>
          <span className="help-link-text">{t('faqs')}</span>
        </Link>

        <Link
          href="/contact"
          className="display-flex flex-column flex-align-center text-no-underline text-ink padding-2 radius-lg hover:opacity-80"
        >
          <div>
            <Image
              src={`/images/states/${state}/icons/contact-icon.svg`}
              alt=""
              width={109}
              height={109}
              aria-hidden="true"
            />
          </div>
          <span className="help-link-text">{t('contactUs')}</span>
        </Link>
      </div>
    </section>
  )
}
