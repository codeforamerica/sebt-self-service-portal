'use client'

import { getState, getStateConfig, getStateLinks } from '@sebt/design-system'
import Link from 'next/link'
import { useTranslation } from 'react-i18next'

export default function NotFound() {
  const { t } = useTranslation('404Portal')
  const state = getState()
  const { pageTitleText } = getStateConfig(state)
  const links = getStateLinks(state)

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1 className={`font-sans-xl ${pageTitleText}`}>{t('title')}</h1>
        <p>{t('body1')}</p>
        <div className="display-flex flex-wrap flex-align-center margin-top-4">
          <Link
            href="/dashboard"
            className="usa-button usa-button--outline margin-right-2"
            data-analytics-cta="not_found_dashboard"
          >
            {t('action1')}
          </Link>
          <Link
            href={links.help.contactUs}
            // margin-right-0 clears the usa-button default trailing margin, which
            // otherwise pushes the row past a 375px viewport and wraps this button
            className="usa-button margin-right-0"
            data-analytics-cta="not_found_contact_us"
            data-analytics-cta-destination-type="external_only"
          >
            {t('action2')}
          </Link>
        </div>
      </div>
    </div>
  )
}
