'use client'

import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import { useFeatureFlag } from '@/features/feature-flags'

import type { Application, Child } from '../../api'
import { CardStatusTimeline } from '../CardStatusTimeline'

interface ChildCardProps {
  child: Child
  application: Application
  id: string
  defaultExpanded?: boolean
}

function formatDate(isoDate: string, locale: string): string {
  const date = new Date(isoDate)
  return new Intl.DateTimeFormat(locale, {
    month: '2-digit',
    day: '2-digit',
    year: 'numeric',
    timeZone: 'UTC'
  }).format(date)
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - {Key}"
export function ChildCard({ child, application, id, defaultExpanded = true }: ChildCardProps) {
  const { t, i18n } = useTranslation('dashboard')
  const showCardLast4 = useFeatureFlag('show_card_last4')
  const [isExpanded, setIsExpanded] = useState(defaultExpanded)
  const childName = `${child.firstName} ${child.lastName}`

  const { benefitIssueDate, benefitExpirationDate, last4DigitsOfCard, issuanceType } = application

  // Map issuance type to i18n key (keys from CSV: cardTableType{Sebt|Snap|Tanf})
  const getCardTypeKey = (type: string | null | undefined): string | null => {
    switch (type) {
      case 'SnapEbtCard':
        return 'cardTableTypeSnap'
      case 'TanfEbtCard':
        return 'cardTableTypeTanf'
      case 'SummerEbt':
        return 'cardTableTypeSebt'
      default:
        return null
    }
  }

  const cardTypeKey = getCardTypeKey(issuanceType)

  const handleToggle = () => {
    setIsExpanded((prev) => !prev)
  }

  return (
    <div className="usa-accordion__item">
      <h3 className="usa-accordion__heading">
        <button
          type="button"
          className="usa-accordion__button"
          aria-expanded={isExpanded}
          aria-controls={`child-${id}`}
          onClick={handleToggle}
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
          {cardTypeKey && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingCardType')}</dt>
              <dd className="margin-left-0">{t(cardTypeKey)}</dd>
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
          {showCardLast4 && last4DigitsOfCard && (
            <>
              <dt className="text-bold margin-top-2">{t('cardTableHeadingCardNumber')}</dt>
              <dd className="margin-left-0">
                {t('cardTableLastFourDigits').replace('[9999]', last4DigitsOfCard)}
              </dd>
            </>
          )}
          <CardStatusTimeline application={application} />
        </dl>
      </div>
    </div>
  )
}
