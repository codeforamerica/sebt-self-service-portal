'use client'

import Link from 'next/link'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { isWithinCooldownPeriod } from '@/features/cards/utils/cooldown'
import { useFeatureFlag } from '@/features/feature-flags'
import { getState } from '@sebt/design-system'

import type { IssuanceType, SummerEbtCase } from '../../api'
import { formatDate } from '../../api'
import { CardStatusDisplay } from '../CardStatusDisplay'
import { CardStatusTimeline } from '../CardStatusTimeline'

/**
 * DC cards always originate as 'Requested' and carry a cardRequestedAt timestamp.
 * CO cards are issued directly without a Requested stage, so cardRequestedAt is absent.
 * This is the data-driven discriminator between the two card lifecycle models.
 */
function hasCardLifecycleTimeline(summerEbtCase: SummerEbtCase): boolean {
  return summerEbtCase.cardRequestedAt != null
}

function getReplacementLink(summerEbtCase: SummerEbtCase): string | null {
  const { summerEBTCaseID, issuanceType, cardRequestedAt } = summerEbtCase
  if (!summerEBTCaseID) return null
  if (!issuanceType || issuanceType === 'Unknown') return null

  if (isWithinCooldownPeriod(cardRequestedAt)) return null

  const currentState = getState()
  const isCoLoaded = issuanceType === 'TanfEbtCard' || issuanceType === 'SnapEbtCard'

  if (isCoLoaded && currentState === 'dc') {
    return '/cards/info'
  }

  if (isCoLoaded) return null

  return `/cards/replace?case=${encodeURIComponent(summerEBTCaseID)}`
}

interface ChildCardProps {
  summerEbtCase: SummerEbtCase
  defaultExpanded?: boolean
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - cardTableType{Sebt|Snap|Tanf}"
const CARD_TYPE_KEYS: Partial<Record<IssuanceType, string>> = {
  SnapEbtCard: 'cardTableTypeSnap',
  TanfEbtCard: 'cardTableTypeTanf',
  SummerEbt: 'cardTableTypeSebt'
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - {Key}"
export function ChildCard({ summerEbtCase, defaultExpanded = true }: ChildCardProps) {
  const { t, i18n } = useTranslation('dashboard')
  const enableCardReplacement = useFeatureFlag('enable_card_replacement')
  const showCaseNumber = useFeatureFlag('show_case_number')
  const showCardLast4 = useFeatureFlag('show_card_last4')
  const [isExpanded, setIsExpanded] = useState(defaultExpanded)
  const childName = `${summerEbtCase.childFirstName} ${summerEbtCase.childLastName}`
  const id = summerEbtCase.summerEBTCaseID ?? ''

  const {
    ebtCaseNumber,
    benefitAvailableDate,
    benefitExpirationDate,
    ebtCardLastFour,
    ebtCardStatus,
    issuanceType,
    cardRequestedAt,
    cardMailedAt,
    cardDeactivatedAt
  } = summerEbtCase
  const cardTypeKey = issuanceType ? (CARD_TYPE_KEYS[issuanceType] ?? null) : null
  const replacementLink = enableCardReplacement ? getReplacementLink(summerEbtCase) : null

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
          {showCaseNumber && ebtCaseNumber && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingSebtId')}</dt>
              <dd className="margin-left-0">{ebtCaseNumber}</dd>
            </>
          )}
          {benefitAvailableDate && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingIssued')}</dt>
              <dd className="margin-left-0">{formatDate(benefitAvailableDate, i18n.language)}</dd>
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
          {showCardLast4 && ebtCardLastFour && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingCardNumber')}</dt>
              <dd className="margin-left-0">
                {t('cardTableLastFourDigits').replace('[9999]', ebtCardLastFour)}
              </dd>
            </>
          )}
          {hasCardLifecycleTimeline(summerEbtCase) ? (
            <CardStatusTimeline
              cardStatus={ebtCardStatus}
              cardRequestedAt={cardRequestedAt}
              cardMailedAt={cardMailedAt}
              cardDeactivatedAt={cardDeactivatedAt}
            />
          ) : (
            <CardStatusDisplay cardStatus={ebtCardStatus} />
          )}
        </dl>
        {replacementLink && (
          <Link
            href={replacementLink}
            data-analytics-cta="replacement_card_cta"
            className="usa-link display-inline-block margin-top-2"
          >
            {t('cardTableActionRequestReplacement', 'Request a replacement card')}
          </Link>
        )}
      </div>
    </div>
  )
}
