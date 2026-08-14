'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getStateLinks } from '../../lib/links'
import type { StateCode } from '../../lib/state'

/**
 * How each state's S.11 mockups arrange the two action buttons: 'stacked' puts
 * them in a column with a full-width outline button (capped at desktop widths,
 * where the mockups don't reach); 'row' keeps both at content width side by
 * side. The exhaustive Record makes adding a StateCode a compile error until
 * the new state declares its layout.
 */
const actionLayoutByState: Record<StateCode, 'stacked' | 'row'> = {
  co: 'stacked',
  dc: 'row',
}

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
  const stacked = actionLayoutByState[state] === 'stacked'

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
            className={`usa-button usa-button--outline${stacked ? ' width-full desktop:width-auto' : ' margin-right-2'}`}
          >
            {t('action1')}
          </Link>
          <Link
            href={links.help.contactUs}
            // margin-right-0 clears the usa-button default trailing margin, which
            // otherwise pushes the row past a 375px viewport and wraps this button
            className={`usa-button${stacked ? ' margin-top-2' : ' margin-right-0'}`}
          >
            {t('action2')}
          </Link>
        </div>
      </div>
    </section>
  )
}
