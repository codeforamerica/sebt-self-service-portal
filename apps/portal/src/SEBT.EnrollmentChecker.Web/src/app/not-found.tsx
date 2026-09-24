'use client'

import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'
import { useTranslation } from 'react-i18next'

import { useApplyHref } from '@/lib/useApplyHref'

export default function NotFound() {
  const { t } = useTranslation('404EnrollmentChecker')
  const { pageTitleText } = getStateConfig(getState())
  const applyHref = useApplyHref()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1 className={`font-family-sans font-sans-xl ${pageTitleText}`}>{t('title')}</h1>
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
