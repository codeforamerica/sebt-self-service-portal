'use client'

import { RichText } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

interface ApplicationAvailableProps {
  /** Application destination, or null when none is configured. */
  applyHref: string | null
}

/**
 * The apply-by deadline and how to act on it.
 *
 * The paper-application note stands on its own: libraries stock forms whether
 * or not an online destination is configured, so it survives when the online
 * link degrades away.
 */
export function ApplicationAvailable({ applyHref }: ApplicationAvailableProps) {
  const { t } = useTranslation('result')
  const { t: tCommon } = useTranslation('common')

  return (
    <section
      className="margin-top-4"
      data-testid="application-available"
    >
      <h2 className="font-family-sans font-sans-md">{t('applyForSebtCard1Title')}</h2>
      <div className="usa-prose margin-top-2">
        <RichText>{t('applyForSebtCard1Body')}</RichText>
      </div>

      {applyHref && (
        <p>
          <a
            href={applyHref}
            className="usa-button"
            data-analytics-cta="apply_cta"
            data-testid="apply-online-link"
          >
            {tCommon('applyOnline')}
          </a>
        </p>
      )}

      <div className="usa-prose margin-top-2">
        <RichText>{t('applyForSebtLibraryApplications')}</RichText>
      </div>
    </section>
  )
}
