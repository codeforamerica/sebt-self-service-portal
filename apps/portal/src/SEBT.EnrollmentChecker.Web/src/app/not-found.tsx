'use client'

import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'
import { useTranslation } from 'react-i18next'

import { useApplyHref } from '@/lib/useApplyHref'
import { PageHeading } from '@sebt/design-system/src/components/ui/PageHeading'

export default function NotFound() {
  const { t } = useTranslation('404EnrollmentChecker')
  const { pageTitleText } = getStateConfig(getState())
  const applyHref = useApplyHref()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <PageHeading className={`font-family-sans font-sans-xl ${pageTitleText}`}>
          {t('title')}
        </PageHeading>
        <p>{t('body1')}</p>
        {applyHref && (
          <a
            href={applyHref}
            className="usa-button margin-top-4"
            data-analytics-cta="apply_cta"
          >
            {t('action')}
          </a>
        )}
      </div>
    </div>
  )
}
