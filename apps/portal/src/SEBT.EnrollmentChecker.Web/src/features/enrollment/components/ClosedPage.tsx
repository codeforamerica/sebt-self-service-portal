'use client'

import { Button, RichText } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { getLandingActions, getLandingConfig } from '@/lib/landingConfig'
import { useEnrollment } from '../context/EnrollmentContext'
import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'

/**
 * Post-season landing page: enrollment has ended but the check still works, so
 * the framing shifts to "was my student enrolled?" and the apply path is gone.
 *
 * Copy lives in the `landing` namespace under `closed*` keys — same screen,
 * different season, edited together in the content sheet.
 *
 * States shape the tail differently and their content follows suit: DC ends with
 * two standalone notes, CO shadows its open page with an accordion wrapping an
 * explanation, a list, and a prompt. `useAccordion` picks between them.
 */
export function ClosedPage() {
  const { t } = useTranslation('landing')
  const { pageTitleText } = getStateConfig(getState())
  const router = useRouter()
  const { clearState } = useEnrollment()
  const [isAccordionExpanded, setIsAccordionExpanded] = useState(false)

  // Arriving here from a deep link should not resume a half-finished check.
  useEffect(() => {
    clearState()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- run once on mount
  }, [])

  const { useAccordion } = getLandingConfig()
  const actions = getLandingActions('closed')

  // \n-delimited list items in accordion states — split and filter empties
  const enrollmentReasons = t('closedBody3').split('\n').filter(Boolean)

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1 className={`font-family-sans ${pageTitleText}`}>{t('closedTitle')}</h1>

        <h2 className="font-family-sans font-sans-md">{t('closedSubtitle')}</h2>

        <div className="usa-prose">
          <RichText>{t('closedBody')}</RichText>
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

        {useAccordion ? (
          <div className="usa-accordion margin-top-4">
            <h2 className="usa-accordion__heading">
              <button
                type="button"
                className="usa-accordion__button bg-transparent border-0"
                aria-expanded={isAccordionExpanded}
                aria-controls="closed-faq-content"
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
                  {t('closedAccordionTitle')}
                </span>
              </button>
            </h2>
            <div
              id="closed-faq-content"
              className="usa-accordion__content usa-prose"
              hidden={!isAccordionExpanded}
            >
              <RichText>{t('closedBody2')}</RichText>
              {enrollmentReasons.length > 0 && (
                <ul className="usa-list margin-top-2">
                  {enrollmentReasons.map((item, index) => (
                    <li key={index}><RichText>{item}</RichText></li>
                  ))}
                </ul>
              )}
              <RichText>{t('closedBody4')}</RichText>
              <p className="margin-top-2">{t('closedBody6')}</p>
            </div>
          </div>
        ) : (
          <div className="usa-prose margin-top-4">
            <RichText>{t('closedBody2')}</RichText>
            <RichText>{t('closedBody3')}</RichText>
          </div>
        )}
      </div>
    </div>
  )
}
