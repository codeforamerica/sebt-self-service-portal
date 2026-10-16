'use client'

import { Button, Details, RichText } from '@sebt/design-system'
import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'
import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { AdentifiPixels } from '@sebt/analytics'
import { getCheckerAssetPath } from '@/lib/checkerAssetPath'
import { getClientConfig } from '@/lib/client-config'
import { getLandingActions, getLandingConfig } from '@/lib/landingConfig'
import { useEnrollment } from '../context/EnrollmentContext'

export function LandingPage() {
  const { t } = useTranslation('landing')
  const { adentifiPixelLanding } = getClientConfig()
  const router = useRouter()
  const { clearState } = useEnrollment()

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
        <h1 className={`font-family-sans font-sans-xl margin-bottom-4 ${pageTitleText}`}>
          {t('title')}
        </h1>
        <div className="usa-prose">
          <RichText>{t('body')}</RichText>
        </div>

        {/* The primary action and the translated group each start a new block;
            further translated actions sit tight together as one group. */}
        {actions.map((action, index) => (
          <div
            key={action.language}
            className={index <= 1 ? 'margin-top-3' : 'margin-top-1'}
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

        {adentifiPixelLanding && (
          <AdentifiPixels pixelId={adentifiPixelLanding} />
        )}

        {useAccordion ? (
          <Details
            className="margin-top-3"
            summary={t('accordionTitle')}
            icon={
              <svg
                className="usa-icon margin-right-1"
                aria-hidden="true"
                focusable="false"
                role="img"
              >
                <use xlinkHref="/img/sprite.svg#info" />
              </svg>
            }
          >
            {eligibilityExplanation}
          </Details>
        ) : (
          <div className="usa-prose margin-top-3">{eligibilityExplanation}</div>
        )}
      </div>
    </div>
  )
}
