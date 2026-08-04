'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getStateLinks } from '../../lib/links'
import type { StateCode } from '../../lib/state'

export interface MaintenancePageContentProps {
  /**
   * Locale namespace carrying this app's S11 maintenance copy. The portal and
   * the enrollment checker generate separate namespaces from the same content
   * sheet, so each app names its own.
   */
  namespace: 'maintenancePortal' | 'maintenanceEnrollmentChecker'
  state: StateCode
}

/**
 * Body of the maintenance page shown while the app is in a scheduled or manual
 * outage. Renders inside the normal app chrome (header, help section, footer);
 * the OutageGuard in each app decides when users land here.
 */
export function MaintenancePageContent({ namespace, state }: MaintenancePageContentProps) {
  const { t } = useTranslation(namespace)
  const links = getStateLinks(state)
  // CO's design stacks the actions with a full-width outline button; DC keeps them in a row.
  const stacked = state === 'co'

  return (
    <section className="usa-section">
      <div className="grid-container">
        <h1>{t('title')}</h1>
        <p>{t('body1')}</p>
        <div
          className={
            stacked
              ? 'display-flex flex-column flex-align-start margin-top-4'
              : 'display-flex flex-wrap flex-align-center margin-top-4'
          }
        >
          <Link
            href={links.help.sebtMainSite}
            className={`usa-button usa-button--outline${stacked ? ' width-full' : ' margin-right-2'}`}
          >
            {t('action1')}
          </Link>
          <Link
            href={links.help.contactUs}
            className={`usa-button${stacked ? ' margin-top-2' : ''}`}
          >
            {t('action2')}
          </Link>
        </div>
      </div>
    </section>
  )
}
