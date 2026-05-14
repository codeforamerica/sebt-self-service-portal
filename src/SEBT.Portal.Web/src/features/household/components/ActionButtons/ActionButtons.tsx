'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getState, getStateConfig } from '@sebt/design-system'

import type { AllowedActions } from '../../api'

interface ActionButton {
  labelKey: string
  href: string
  ctaId: string
  /** Which allowedActions field gates this CTA. When set, the CTA is hidden if the field is false. */
  gatedBy?: keyof Pick<AllowedActions, 'canUpdateAddress' | 'canRequestReplacementCard'>
  /** When true, the CTA is hidden if hasCases is explicitly false. Omitting hasCases keeps the CTA visible (backward-compatible). */
  requiresCases?: boolean
}

interface ActionButtonsProps {
  /** Server-computed action permissions from the household data response. */
  allowedActions?: AllowedActions | null | undefined
  /** Whether the household has any enrolled children (summerEbtCases.length > 0). When false, CTAs that require cases are hidden. */
  hasCases?: boolean
}

// Keys map to CSV: "S2 - Portal Dashboard - Action Navigation - {Key}"
const ACTIONS: ActionButton[] = [
  {
    labelKey: 'actionNavigationChangeMyMailingAddress',
    href: '/profile/address',
    ctaId: 'update_address_cta',
    gatedBy: 'canUpdateAddress'
  },
  {
    labelKey: 'actionNavigationOrderReplacementCards',
    href: '/cards/request',
    ctaId: 'replacement_card_cta',
    gatedBy: 'canRequestReplacementCard'
  },
  {
    labelKey: 'actionNavigationCheckExistingCards',
    href: '#enrolled-children-heading',
    ctaId: 'check_cards_cta',
    requiresCases: true
  },
  {
    labelKey: 'actionNavigationCheckExistingApplications',
    href: '#applications-heading',
    ctaId: 'check_applications_cta'
  }
]

export function ActionButtons({ allowedActions, hasCases }: ActionButtonsProps) {
  const { t } = useTranslation('dashboard')
  const { actionButtonBg, actionButtonText } = getStateConfig(getState())

  const visibleActions = ACTIONS.filter((action) => {
    if (action.requiresCases && hasCases === false) return false
    if (!action.gatedBy) return true
    // When allowedActions is not provided, default to showing the CTA (backward-compatible).
    if (!allowedActions) return true
    return allowedActions[action.gatedBy]
  })

  return (
    <nav
      className="margin-bottom-4"
      aria-label={t('actionNavigationNavLabel', 'Quick actions')}
    >
      <p className="margin-top-0 margin-bottom-2 text-base-dark">{t('actionNavigationLead')}</p>

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
              {...(action.href.startsWith('#') && {
                onClick: (e: React.MouseEvent) => {
                  e.preventDefault()
                  document
                    .getElementById(action.href.slice(1))
                    ?.scrollIntoView({ behavior: 'smooth' })
                }
              })}
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
