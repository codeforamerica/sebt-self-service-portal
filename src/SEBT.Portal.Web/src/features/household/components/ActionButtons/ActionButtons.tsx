'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getState, getStateConfig } from '@/lib/state'

interface ActionButton {
  labelKey: string
  href: string
}

// Keys map to CSV: "S2 - Portal Dashboard - Action Navigation - {Key}"
const ACTIONS: ActionButton[] = [
  { labelKey: 'actionNavigationCheckExistingCards', href: '/cards' },
  { labelKey: 'actionNavigationOrderReplacementCards', href: '/cards/request' },
  { labelKey: 'actionNavigationChangeMyMailingAddress', href: '/profile/address' },
  { labelKey: 'actionNavigationChangeMyContactInformation', href: '/profile/contact' },
  { labelKey: 'actionNavigationCheckExistingApplications', href: '/applications' }
]

export function ActionButtons() {
  const { t } = useTranslation('dashboard')
  const { actionButtonBg, actionButtonText } = getStateConfig(getState())

  return (
    <nav
      className="margin-bottom-4"
      aria-label={t('actionNavigationNavLabel', 'Quick actions')}
    >
      <p className="margin-top-0 margin-bottom-2 text-base-dark">{t('actionNavigationLead')}</p>
      <ul className="usa-list usa-list--unstyled">
        {ACTIONS.map((action) => (
          <li
            key={action.labelKey}
            className="margin-bottom-2"
          >
            <Link
              href={action.href}
              className={`display-inline-flex flex-align-center padding-y-1 padding-x-205 text-no-underline ${actionButtonText} ${actionButtonBg} radius-pill font-sans-md text-semibold`}
            >
              {t(action.labelKey)}
              <svg
                aria-hidden="true"
                className="margin-left-1"
                width="28"
                height="28"
                viewBox="0 0 24 24"
                fill="currentColor"
              >
                <path d="M10 6 8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z" />
              </svg>
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  )
}
