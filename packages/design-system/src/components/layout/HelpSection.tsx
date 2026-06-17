'use client'

import Image from 'next/image'
import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getHelpLinks, getStateLinks } from '../../lib/links'

import type { HelpSectionProps } from './types'

import type { StateCode } from '../../lib/state'

const helpSectionOverrides: Partial<Record<StateCode, React.ComponentType<HelpSectionProps>>> = {
  co: COHelpSection
}

export function HelpSection({ state = 'dc' }: HelpSectionProps) {
  const { t } = useTranslation('common')

  // eslint-disable-next-line security/detect-object-injection -- state is typed StateCode
  const Override = state ? helpSectionOverrides[state] : undefined
  if (Override) return <Override state={state} />

  const helpLinks = getHelpLinks(state)

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
        {helpLinks.map((link, index) => (
          <Link
            key={link.key}
            href={link.href}
            target="_blank"
            rel="noopener noreferrer"
            className={`display-flex flex-column flex-align-center text-no-underline text-ink radius-lg hover:opacity-80 ${
              index === 0 ? 'padding-top-2' : 'padding-2'
            }`}
          >
            <div>
              <Image
                src={`/images/states/${state}/icons/${link.icon}`}
                alt=""
                width={109}
                height={109}
                aria-hidden="true"
              />
            </div>
            <span className="font-sans-lg text-semibold text-underline">
              {t(link.translationKey)}
            </span>
          </Link>
        ))}
      </div>
    </section>
  )
}

function COHelpSection({ state = 'co' }: HelpSectionProps) {
  const links = getStateLinks(state)
  const { t } = useTranslation('common')

  return (
    <section
      className="co-help-section bg-base-lightest padding-x-3 padding-y-4"
      aria-labelledby="help-section-title"
    >
      <div className="maxw-tablet margin-x-auto">
        <h2
          id="help-section-title"
          className="font-sans-lg text-bold margin-top-0 margin-bottom-1"
        >
          {t('titleFaqs')}
        </h2>

        <Link
          href={links.help.faqs}
          target="_blank"
          rel="noopener noreferrer"
          className="usa-link text-ink font-sans-sm"
        >
          {t('linkFaqsLearnMore')}
        </Link>
      </div>
    </section>
  )
}
