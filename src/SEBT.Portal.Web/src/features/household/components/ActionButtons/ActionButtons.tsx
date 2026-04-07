'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getState, getStateConfig } from '@sebt/design-system'

import type { SummerEbtCase } from '../../api'

interface ActionButton {
  labelKey: string
  href: string
  ctaId: string
  /** When true, this CTA is hidden when no case has a dedicated Summer EBT card. */
  selfServiceOnly?: boolean
}

interface ActionButtonsProps {
  /** Enrolled cases — used to determine which self-service actions are available. */
  cases: SummerEbtCase[]
}

// Keys map to CSV: "S2 - Portal Dashboard - Action Navigation - {Key}"
const ACTIONS: ActionButton[] = [
  {
    labelKey: 'actionNavigationChangeMyMailingAddress',
    href: '/profile/address',
    ctaId: 'update_address_cta',
    selfServiceOnly: true
  },
  {
    labelKey: 'actionNavigationOrderReplacementCards',
    href: '/cards/request',
    ctaId: 'replacement_card_cta',
    selfServiceOnly: true
  },
  {
    labelKey: 'actionNavigationCheckExistingCards',
    href: '/cards',
    ctaId: 'check_cards_cta'
  },
  {
    labelKey: 'actionNavigationCheckExistingApplications',
    href: '/applications',
    ctaId: 'check_applications_cta'
  }
]

/**
 * SNAP and TANF benefit holders cannot use portal self-service features
 * (address update, replacement card) — those actions must go through
 * their case worker.
 */
function isSelfServiceAvailable(cases: SummerEbtCase[]): boolean {
  if (cases.length === 0) return true
  return cases.some(
    (c) => !c.issuanceType || c.issuanceType === 'Unknown' || c.issuanceType === 'SummerEbt'
  )
}

export function ActionButtons({ cases }: ActionButtonsProps) {
  const { t } = useTranslation('dashboard')
  const { actionButtonBg, actionButtonText } = getStateConfig(getState())
  const selfServiceEnabled = isSelfServiceAvailable(cases)

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
              data-analytics-cta={action.ctaId}
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
