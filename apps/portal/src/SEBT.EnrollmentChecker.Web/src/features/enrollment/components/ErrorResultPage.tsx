'use client'

import Image from 'next/image'
import { useTranslation } from 'react-i18next'

import { RichText } from '@sebt/design-system'

interface ErrorResultPageProps {
  /** Undefined on a misconfigured deployment; the CTA is hidden rather than dead. */
  portalUrl: string | undefined
}

/**
 * Full-page treatment for a whole-check submit failure: the system could not
 * share enrollment information at all. Offers the portal as the next step and
 * deliberately carries no application steps or links (applications are closed,
 * DC-701).
 */
export function ErrorResultPage({ portalUrl }: ErrorResultPageProps) {
  const { t } = useTranslation('result')

  return (
    <div className="usa-section">
      <div className="grid-container">
        <Image
          src={`${process.env.NEXT_PUBLIC_BASE_PATH}/images/states/co/icon-alert-card.svg`}
          alt=""
          width={100}
          height={75}
          aria-hidden="true"
        />
        <h1 className="font-family-sans text-primary margin-top-1">{t('errorTitle')}</h1>
        <p>{t('errorBody')}</p>

        <section data-testid="next-step-portal">
          <h2 className="font-family-sans margin-top-4">{t('streamlinedEnrolledAlertTitle')}</h2>
          <div className="margin-top-2">
            <RichText>{t('streamlinedEnrolledAlertBody')}</RichText>
          </div>
          {portalUrl && (
            <p>
              <a
                href={portalUrl}
                className="usa-button"
                data-testid="portal-link"
              >
                {t('streamlinedEnrolledAction')}
              </a>
            </p>
          )}
        </section>
      </div>
    </div>
  )
}
