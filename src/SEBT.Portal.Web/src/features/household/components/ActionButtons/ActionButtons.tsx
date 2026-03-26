'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getState, getStateConfig } from '@sebt/design-system'

import type { IssuanceType } from '../../api'

interface ActionButton {
  labelKey: string
  href: string
  /** When true, this CTA is hidden for SNAP/TANF issuance types. */
  selfServiceOnly?: boolean
}

interface ActionButtonsProps {
  /** The benefit issuance type determines which self-service actions are available. */
  issuanceType?: IssuanceType | null | undefined
}

// Keys map to CSV: "S2 - Portal Dashboard - Action Navigation - {Key}"
const ACTIONS: ActionButton[] = [
  {
    labelKey: 'actionNavigationChangeMyMailingAddress',
    href: '/profile/address',
    selfServiceOnly: true
  },
  {
    labelKey: 'actionNavigationOrderReplacementCards',
    href: '/cards/request',
    selfServiceOnly: true
  },
  { labelKey: 'actionNavigationCheckExistingCards', href: '/cards' },
  { labelKey: 'actionNavigationCheckExistingApplications', href: '/applications' }
]

/**
 * SNAP and TANF benefit holders cannot use portal self-service features
 * (address update, replacement card) — those actions must go through
 * their case worker.
 */
function isSelfServiceAvailable(issuanceType?: IssuanceType | null): boolean {
  if (!issuanceType) return true
  return issuanceType !== 'SnapEbtCard' && issuanceType !== 'TanfEbtCard'
}

export function ActionButtons({ issuanceType }: ActionButtonsProps) {
  const { t } = useTranslation('dashboard')
  const { actionButtonBg, actionButtonText } = getStateConfig(getState())
  const selfServiceEnabled = isSelfServiceAvailable(issuanceType)

  const visibleActions = ACTIONS.filter((action) => !action.selfServiceOnly || selfServiceEnabled)

  return (
    <nav
      className="margin-bottom-4"
      aria-label={t('actionNavigationNavLabel', 'Quick actions')}
    >
      <p className="margin-top-0 margin-bottom-2 text-base-dark">{t('actionNavigationLead')}</p>

      {!selfServiceEnabled && (
        <div
          className="usa-alert usa-alert--info usa-alert--slim margin-bottom-2"
          role="status"
        >
          <div className="usa-alert__body">
            <p className="usa-alert__text">{t('actionNavigationSelfServiceUnavailable')}</p>
          </div>
        </div>
      )}

      <ul className="usa-list usa-list--unstyled">
        {visibleActions.map((action) => (
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
