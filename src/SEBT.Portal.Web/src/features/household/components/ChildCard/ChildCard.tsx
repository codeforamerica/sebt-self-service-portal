'use client'

import Link from 'next/link'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { isWithinCooldownPeriod } from '@/features/cards/utils/cooldown'
import { useFeatureFlag } from '@/features/feature-flags'
import { getState } from '@sebt/design-system'

import type { Application, Child, IssuanceType } from '../../api'
import { formatDate } from '../../api'
import { CardStatusDisplay } from '../CardStatusDisplay'
import { CardStatusTimeline } from '../CardStatusTimeline'

/**
 * DC cards always originate as 'Requested' and carry a cardRequestedAt timestamp.
 * CO cards are issued directly without a Requested stage, so cardRequestedAt is absent.
 * This is the data-driven discriminator between the two card lifecycle models.
 */
function hasDcCardLifecycle(application: Application): boolean {
  return application.cardRequestedAt != null
}

function getReplacementLink(application: Application): string | null {
  const { applicationNumber, issuanceType, cardRequestedAt } = application
  if (!applicationNumber) return null

  if (isWithinCooldownPeriod(cardRequestedAt)) return null

  const currentState = getState()
  const isCoLoaded = issuanceType === 'TanfEbtCard' || issuanceType === 'SnapEbtCard'

  if (isCoLoaded && currentState === 'dc') {
    return '/cards/info'
  }

  if (isCoLoaded) return null

  return `/cards/replace?app=${encodeURIComponent(applicationNumber)}`
}

interface ChildCardProps {
  child: Child
  application: Application
  id: string
  defaultExpanded?: boolean
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - cardTableType{Sebt|Snap|Tanf}"
const CARD_TYPE_KEYS: Partial<Record<IssuanceType, string>> = {
  SnapEbtCard: 'cardTableTypeSnap',
  TanfEbtCard: 'cardTableTypeTanf',
  SummerEbt: 'cardTableTypeSebt'
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - {Key}"
export function ChildCard({ child, application, id, defaultExpanded = true }: ChildCardProps) {
  const { t, i18n } = useTranslation('dashboard')
  const enableCardReplacement = useFeatureFlag('enable_card_replacement')
  const showCaseNumber = useFeatureFlag('show_case_number')
  const showCardLast4 = useFeatureFlag('show_card_last4')
  const [isExpanded, setIsExpanded] = useState(defaultExpanded)
  const childName = `${child.firstName} ${child.lastName}`

  const { caseNumber, benefitIssueDate, benefitExpirationDate, last4DigitsOfCard, issuanceType } =
    application
  const cardTypeKey = issuanceType ? (CARD_TYPE_KEYS[issuanceType] ?? null) : null
  const replacementLink = enableCardReplacement ? getReplacementLink(application) : null

  return (
    <div className="usa-accordion__item">
      <h3 className="usa-accordion__heading">
        <button
          type="button"
          className="usa-accordion__button"
          aria-expanded={isExpanded}
          aria-controls={`child-${id}`}
          onClick={() => setIsExpanded((prev) => !prev)}
        >
          {childName}
        </button>
      </h3>
      <div
        id={`child-${id}`}
        className="usa-accordion__content usa-prose"
        hidden={!isExpanded}
        data-testid="accordion-content"
      >
        <dl className="margin-0">
          {showCaseNumber && caseNumber && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingSebtId')}</dt>
              <dd className="margin-left-0">{caseNumber}</dd>
            </>
          )}
          {benefitIssueDate && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingIssued')}</dt>
              <dd className="margin-left-0">{formatDate(benefitIssueDate, i18n.language)}</dd>
            </>
          )}
          {benefitExpirationDate && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingExpDate')}</dt>
              <dd className="margin-left-0">{formatDate(benefitExpirationDate, i18n.language)}</dd>
            </>
          )}
          {cardTypeKey && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingCardType')}</dt>
              <dd className="margin-left-0">{t(cardTypeKey)}</dd>
            </>
          )}
          {showCardLast4 && last4DigitsOfCard && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingCardNumber')}</dt>
              <dd className="margin-left-0">
                {t('cardTableLastFourDigits').replace('[9999]', last4DigitsOfCard)}
              </dd>
            </>
          )}
          {hasDcCardLifecycle(application) ? (
            <CardStatusTimeline application={application} />
          ) : (
            <CardStatusDisplay application={application} />
          )}
        </dl>
        {replacementLink && (
          <Link
            href={replacementLink}
            className="usa-link display-inline-block margin-top-2"
          >
            {t('cardTableActionRequestReplacement', 'Request a replacement card')}
          </Link>
        )}
      </div>
    </div>
  )
}
