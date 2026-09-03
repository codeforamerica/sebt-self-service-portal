'use client'

import { getCheckerAssetPath } from '@/lib/checkerAssetPath'
import { allowsSequentialChecks } from '@/lib/flowConfig'
import { useEnrollmentSeason } from '@/lib/useEnrollmentSeason'
import Image from 'next/image'
import { useTranslation } from 'react-i18next'

import { RichText } from '@sebt/design-system'
import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'
import { CheckAnotherChildCard } from './CheckAnotherChildCard'

interface ErrorResultPageProps {
  portalUrl: string
}

/**
 * Full-page treatment for a whole-check submit failure: the system could not
 * share enrollment information at all. Offers the portal as the next step and
 * deliberately carries no application steps or links (applications are closed,
 * DC-701).
 */
export function ErrorResultPage({ portalUrl }: ErrorResultPageProps) {
  const { t } = useTranslation('result')
  const { pageTitleText } = getStateConfig(getState())
  const errorCard = getCheckerAssetPath('errorCard')

  // The failure reads the same in either season; only the portal explanation
  // underneath it moves into the past tense.
  const { season } = useEnrollmentSeason()
  const isClosed = season === 'closed'
  const alertKey = (suffix: string) => `streamlinedEnrolled${isClosed ? 'Closed' : ''}${suffix}`

  return (
    <div className="usa-section">
      <div className="grid-container">
        {errorCard && (
          <Image
            src={errorCard}
            alt=""
            width={100}
            height={75}
            aria-hidden="true"
          />
        )}
        <h1 className={`font-family-sans font-sans-xl ${pageTitleText} margin-top-1`}>{t('errorTitle')}</h1>
        <p>{t('errorBody')}</p>

        <section data-testid="next-step-portal">
          {isClosed ? (
            /* Past season: the portal pointer is a link at the head of the
               explanation rather than a heading above it. */
            <div className="usa-prose margin-top-4">
              <p>
                <a
                  href={portalUrl}
                  className="text-bold"
                  data-testid="portal-alert-link"
                >
                  {t(alertKey('AlertTitle'))}
                </a>
              </p>
              <RichText>{t(alertKey('AlertBody'))}</RichText>
            </div>
          ) : (
            <>
              <h2 className="font-family-sans margin-top-4">{t(alertKey('AlertTitle'))}</h2>
              <div className="margin-top-2">
                <RichText>{t(alertKey('AlertBody'))}</RichText>
              </div>
            </>
          )}
          <p>
            <a
              href={portalUrl}
              className="usa-button"
              data-testid="portal-link"
            >
              {t('streamlinedEnrolledAction')}
            </a>
          </p>
        </section>

        {/* A failed check in a closed season is otherwise a dead end — there is no
            application to fall back on, so the only move left is another check. */}
        {isClosed && allowsSequentialChecks() && (
          <CheckAnotherChildCard
            copy="streamlinedEnrolledCard2"
            bodyKey="applyForSebtClosedCard2Body"
          />
        )}
      </div>
    </div>
  )
}
