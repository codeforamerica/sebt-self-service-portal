'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getState, getStateConfig } from '@sebt/design-system'

import { getApplyHref } from '@/lib/applyHref'

import type { AllowedActions } from '../../api'

interface ActionButton {
  labelKey: string
  /** Static destination. Mutually exclusive with resolveHref. */
  href?: string
  /** Destination computed at render time (e.g. the per-state, locale-aware apply URL). */
  resolveHref?: (locale: string) => string
  /** When true, renders an outbound <a> (same tab) instead of a client-side <Link>. */
  external?: boolean
  ctaId: string
  /** Which allowedActions field gates this CTA. When set, the CTA is hidden if the field is false. */
  gatedBy?: keyof Pick<AllowedActions, 'canUpdateAddress' | 'canRequestReplacementCard'>
  /** When true, the CTA is hidden if hasCases is explicitly false. Omitting hasCases keeps the CTA visible (backward-compatible). */
  requiresCases?: boolean
  /** When set, the CTA renders only for these states. Omitting it shows the CTA for every state. */
  states?: string[]
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
    // Outbound link to the state's apply form; shown for both states, always.
    labelKey: 'actionNavigationApply',
    resolveHref: getApplyHref,
    external: true,
    ctaId: 'apply_cta'
  },
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
  },
  {
    // CO-only for now: the authored label exists for CO, while DC's is still !N/A!
    // upstream. Add 'dc' once the DC content is published (see DC-162 follow-up).
    labelKey: 'actionNavigationActivateCard',
    href: '/cards/activate',
    ctaId: 'activate_card_cta',
    requiresCases: true,
    states: ['co']
  }
]

export function ActionButtons({ allowedActions, hasCases }: ActionButtonsProps) {
  const { t, i18n } = useTranslation('dashboard')
  const currentState = getState()
  const { actionButtonBg, actionButtonText } = getStateConfig(currentState)

  const visibleActions = ACTIONS.filter((action) => {
    if (action.states && !action.states.includes(currentState)) return false
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
        {visibleActions.map((action) => {
          const href = action.resolveHref ? action.resolveHref(i18n.language) : (action.href ?? '')
          const className = `display-inline-flex flex-align-center padding-y-1 padding-x-205 text-no-underline ${actionButtonText} ${actionButtonBg} radius-pill font-sans-md text-semibold`
          const content = (
            <>
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
            </>
          )

          return (
            <li
              key={action.labelKey}
              className="margin-bottom-2"
            >
              {action.external ? (
                <a
                  href={href}
                  data-analytics-cta={action.ctaId}
                  data-analytics-cta-destination-type="external_only"
                  className={className}
                >
                  {content}
                </a>
              ) : (
                <Link
                  href={href}
                  data-analytics-cta={action.ctaId}
                  className={className}
                  {...(href.startsWith('#') && {
                    onClick: (e: React.MouseEvent) => {
                      e.preventDefault()
                      document.getElementById(href.slice(1))?.scrollIntoView({ behavior: 'smooth' })
                    }
                  })}
                >
                  {content}
                </Link>
              )}
            </li>
          )
        })}
      </ul>
    </nav>
  )
}
