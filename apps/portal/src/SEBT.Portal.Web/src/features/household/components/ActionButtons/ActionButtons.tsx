'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getState, getStateConfig } from '@sebt/design-system'

import { useFeatureFlag } from '@/features/feature-flags'
import { useApplyHref } from '@/lib/useApplyHref'

import type { AllowedActions } from '../../api'

interface ActionButton {
  labelKey: string
  /** Static destination. Omitted only for the apply CTA, whose URL comes from useApplyHref. */
  href?: string
  /**
   * The apply CTA's destination is resolved at render time by useApplyHref (runtime-configured,
   * locale-aware, gated by the enable_apply feature flag). Null hides the CTA.
   */
  isApplyAction?: boolean
  /** When true, renders an outbound <a> (same tab) instead of a client-side <Link>. */
  external?: boolean
  ctaId: string
  /** Which allowedActions field gates this CTA. When set, the CTA is hidden if the field is false. */
  gatedBy?: keyof Pick<AllowedActions, 'canUpdateAddress' | 'canRequestReplacementCard'>
  /** When true, the CTA is hidden if hasCases is explicitly false. Omitting hasCases keeps the CTA visible (backward-compatible). */
  requiresCases?: boolean
  /** When true, the CTA is hidden if hasApplications is explicitly false. Omitting hasApplications keeps the CTA visible (backward-compatible). */
  requiresApplications?: boolean
  /** When true, the CTA is hidden unless the enable_activate_card feature flag is on. */
  requiresActivateCardFlag?: boolean
  /** When true, the CTA is hidden unless at least one child's benefits are expiring or expired. */
  requiresBenefitExpiration?: boolean
  /** When true, the CTA is hidden in states whose content has no label for it. */
  requiresLabel?: boolean
}

interface ActionButtonsProps {
  /** Server-computed action permissions from the household data response. */
  allowedActions?: AllowedActions | null | undefined
  /** Whether the household has any enrolled children (summerEbtCases.length > 0). When false, CTAs that require cases are hidden. */
  hasCases?: boolean
  /** Whether the household has any applications (applications.length > 0). When false, CTAs that require applications are hidden. */
  hasApplications?: boolean
  /** Whether any child's benefits are expiring or expired. CTAs that require it are hidden otherwise. */
  hasBenefitExpiration?: boolean
}

// Keys map to CSV: "S2 - Portal Dashboard - Action Navigation - {Key}"
const ACTIONS: ActionButton[] = [
  {
    // Outbound link to the apply form. Hidden when applications are closed
    // (enable_apply flag off) or no apply URL is configured.
    labelKey: 'actionNavigationApply',
    isApplyAction: true,
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
    labelKey: 'actionNavigationCheckBenefitExpiration',
    href: '#enrolled-children-heading',
    ctaId: 'check_benefit_expiration_cta',
    requiresBenefitExpiration: true,
    requiresLabel: true
  },
  {
    labelKey: 'actionNavigationCheckExistingApplications',
    href: '#applications-heading',
    ctaId: 'check_applications_cta',
    requiresApplications: true
  },
  {
    // Only CO's sheet authors this label; with the flag on and no label the button shows the
    // raw key, so enable it for another state only once its content is published.
    labelKey: 'actionNavigationActivateCard',
    href: '/cards/activate',
    ctaId: 'activate_card_cta',
    requiresCases: true,
    requiresActivateCardFlag: true
  }
]

export function ActionButtons({
  allowedActions,
  hasCases,
  hasApplications,
  hasBenefitExpiration
}: ActionButtonsProps) {
  const { t } = useTranslation('dashboard')
  const { actionButtonBg, actionButtonText } = getStateConfig(getState())
  const applyHref = useApplyHref()
  const activateCardEnabled = useFeatureFlag('enable_activate_card')

  const visibleActions = ACTIONS.filter((action) => {
    if (action.requiresActivateCardFlag && !activateCardEnabled) return false
    if (action.isApplyAction && applyHref === null) return false
    if (action.requiresCases && hasCases === false) return false
    if (action.requiresApplications && hasApplications === false) return false
    if (action.requiresBenefitExpiration && !hasBenefitExpiration) return false
    if (action.requiresLabel && !t(action.labelKey, { defaultValue: '' })) return false
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
          const href = action.isApplyAction ? (applyHref ?? '') : (action.href ?? '')
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
