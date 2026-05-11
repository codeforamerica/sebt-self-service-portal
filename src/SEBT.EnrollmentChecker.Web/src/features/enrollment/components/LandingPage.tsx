'use client'

import { Button, RichText } from '@sebt/design-system'
import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

export function LandingPage() {
  const { t } = useTranslation('landing')
  const router = useRouter()
  const [isAccordionExpanded, setIsAccordionExpanded] = useState(false)

  // body3 is \n-delimited list items — split and filter empties
  const body3Items = t('body3').split('\n').filter(Boolean)

  return (
    <div className="usa-section">
      <div className="grid-container">
        <Image
          src={`${process.env.NEXT_PUBLIC_BASE_PATH}/images/states/co/summer-ebt-logo.svg`}
          alt="Summer EBT"
          width={287}
          height={33}
          className="margin-bottom-2"
          priority
        />
        <h1 className="font-family-sans">{t('title')}</h1>
        <div className="usa-prose">
          <RichText>{t('body')}</RichText>
        </div>

        <div className="margin-top-3">
          <Button
            onClick={() => router.push('/disclaimer')}
            data-analytics-cta="start_enrollment_check_cta"
          >
            {t('action')}
          </Button>
        </div>
        <div className="margin-top-2">
          <Button
            variant="outline"
            onClick={() => router.push('/disclaimer')}
            data-analytics-cta="start_enrollment_check_cta_es"
          >
            {t('actionEspañol')}
          </Button>
        </div>

        {/* FAQ Accordion — follows USWDS accordion pattern */}
        <div className="usa-accordion margin-top-4">
          <h2 className="usa-accordion__heading">
            <button
              type="button"
              className="usa-accordion__button bg-transparent border-0"
              aria-expanded={isAccordionExpanded}
              aria-controls="faq-content"
              onClick={() => setIsAccordionExpanded((prev) => !prev)}
            >
              <span className="display-flex flex-align-center text-info-darker">
                <svg
                  className="usa-icon margin-right-1"
                  aria-hidden="true"
                  focusable="false"
                  role="img"
                >
                  <use xlinkHref="/img/sprite.svg#info" />
                </svg>
                {t('accordionTitle')}
              </span>
            </button>
          </h2>
          <div
            id="faq-content"
            className="usa-accordion__content usa-prose"
            hidden={!isAccordionExpanded}
          >
            <RichText>{t('body2')}</RichText>
            <ul className="usa-list margin-top-2">
              {body3Items.map((item, index) => (
                <li key={index}>{item}</li>
              ))}
            </ul>
            <RichText>{t('body4')}</RichText>
          </div>
        </div>
      </div>
    </div>
  )
}
