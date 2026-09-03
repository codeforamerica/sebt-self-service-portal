'use client'

import { Button, RichText } from '@sebt/design-system'
import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'
import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AdentifiPixels } from '@sebt/analytics'
import { getCheckerAssetPath } from '@/lib/checkerAssetPath'
import { env } from '@/lib/env'
import { getLandingActions, getLandingConfig } from '@/lib/landingConfig'
import { useEnrollment } from '../context/EnrollmentContext'

export function LandingPage() {
  const { t } = useTranslation('landing')
  const router = useRouter()
  const { clearState } = useEnrollment()
  const [isAccordionExpanded, setIsAccordionExpanded] = useState(false)

  // The landing page is a fresh-start screen — clicking the logo from any
  // deep page lands here, and the cached children should not persist.
  useEffect(() => {
    clearState()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- run once on mount
  }, [])

  // body3 is \n-delimited list items — split and filter empties
  const reaonsForAutoEnrollment = t('body3').split('\n').filter(Boolean)
  const reasonsToApply = t('body5').split('\n').filter(Boolean)

  // Undefined for states with no logo above the heading — DC brands this screen
  // through the toolbar logo instead.
  const landingLogo = getCheckerAssetPath('landingLogo')
  const { programName, pageTitleText } = getStateConfig(getState())

  const { useAccordion } = getLandingConfig()
  const actions = getLandingActions()

  // Shared by both layouts so they can't drift apart.
  const eligibilityExplanation = (
    <>
      <RichText>{t('body2')}</RichText>
      {reaonsForAutoEnrollment.length > 0 && (
        <ul className="usa-list margin-top-2">
          {reaonsForAutoEnrollment.map((item, index) => (
            <li key={index}><RichText>{item}</RichText></li>
          ))}
        </ul>
      )}
      <RichText>{t('body4')}</RichText>
      {reasonsToApply.length > 0 && (
        <ul className="usa-list margin-top-2">
          {reasonsToApply.map((item, index) => (
            <li key={index}><RichText>{item}</RichText></li>
          ))}
        </ul>
      )}
      <p className="margin-top-2">{t('body6')}</p>
    </>
  )

  return (
    <div className="usa-section">
      <div className="grid-container">
        {landingLogo && (
          <Image
            src={landingLogo}
            alt={programName}
            width={287}
            height={33}
            className="margin-bottom-2"
            priority
          />
        )}
        <h1 className={`font-family-sans ${pageTitleText}`}>{t('title')}</h1>
        <div className="usa-prose">
          <RichText>{t('body')}</RichText>
        </div>

        {actions.map((action, index) => (
          <div
            key={action.language}
            className={index === 0 ? 'margin-top-3' : 'margin-top-2'}
          >
            <Button
              // Spread, not `variant={action.variant}` — exactOptionalPropertyTypes
              // rejects an explicit undefined, so the filled button omits the prop.
              {...(action.variant && { variant: action.variant })}
              onClick={() => router.push('/disclaimer')}
              data-analytics-cta={action.analyticsCta}
            >
              {t(action.translationKey)}
            </Button>
          </div>
        ))}

        {env.NEXT_PUBLIC_ADENTIFI_PIXEL_LANDING && (
          <AdentifiPixels pixelId={env.NEXT_PUBLIC_ADENTIFI_PIXEL_LANDING} />
        )}

        {/* FAQ Accordion — follows USWDS accordion pattern */}
        {useAccordion ? (
          <div className="usa-accordion margin-top-4">
            <h2 className="usa-accordion__heading">
              <button
                type="button"
                className="usa-accordion__button bg-transparent border-0"
                aria-expanded={isAccordionExpanded}
                aria-controls="faq-content"
                onClick={() => setIsAccordionExpanded((prev) => !prev)}
              >
                <span className="display-flex flex-align-center text-primary">
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
              {eligibilityExplanation}
            </div>
          </div>
        ) : (
          <div className="usa-prose margin-top-4">{eligibilityExplanation}</div>
        )}
      </div>
    </div>
  )
}
