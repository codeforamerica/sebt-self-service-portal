'use client'

import { RichText } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

import Image from 'next/image'
import type { CardStatus, UiCardStatus } from '../../api'
import { interpolateDate, toUiCardStatus } from '../../api'

interface CardStatusDisplayProps {
  cardStatus: CardStatus | null | undefined
  cardIssuedAt?: string | null | undefined
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - Status {Status}"
const CARD_STATUS_CONFIG: Record<
  UiCardStatus,
  { colorClass: string; labelKey: string; icon: string }
> = {
  Active: { colorClass: 'info', labelKey: 'cardTableStatusActive', icon: 'credit_card_check.svg' },
  Processed: { colorClass: 'success', labelKey: 'cardTableStatusIssued', icon: 'mail_rounded.svg' },
  Inactive: {
    colorClass: 'bg-base-lightest',
    labelKey: 'cardTableStatusInactive',
    icon: 'credit_card_off.svg'
  },
  Frozen: {
    colorClass: 'bg-base-lightest',
    labelKey: 'cardTableStatusFrozen',
    icon: 'lock_outline.svg'
  },
  Undeliverable: {
    colorClass: 'warning',
    labelKey: 'cardTableStatusUndeliverable',
    icon: 'warning.svg'
  }
}

const DESCRIPTION_KEY: Partial<Record<CardStatus, string>> = {
  Active: 'cardTableStatusMessageActive',
  Processed: 'cardTableStatusMessageProcessed',
  Lost: 'cardTableStatusMessageInactive',
  Stolen: 'cardTableStatusMessageInactive',
  Damaged: 'cardTableStatusMessageInactive',
  DeactivatedByState: 'cardTableStatusMessageDeactivated',
  NotActivated: 'cardTableStatusMessageDeactivated',
  Frozen: 'cardTableStatusMessageFrozen',
  Undeliverable: 'cardTableStatusMessageUndeliverable'
}

export function CardStatusDisplay({ cardStatus, cardIssuedAt }: CardStatusDisplayProps) {
  const { t, i18n } = useTranslation('dashboard')

  if (!cardStatus || cardStatus === 'Unknown') return null

  const uiStatus = toUiCardStatus(cardStatus)
  const { colorClass, labelKey, icon } = CARD_STATUS_CONFIG[uiStatus]
  const statusLabel = interpolateDate(
    t(labelKey, { defaultValue: '' }),
    cardIssuedAt ?? null,
    i18n.language
  )
  const descriptionKey = DESCRIPTION_KEY[cardStatus] ?? 'cardTableStatusMessageInactive'
  const statusDescription = t(descriptionKey, { defaultValue: '' })

  return (
    <div className="margin-top-2">
      <dt className="text-bold">{t('cardTableHeadingCardStatus')}</dt>
      <dd className="margin-left-0 margin-top-1">
        <div
          className={`display-flex flex-align-center padding-1 border-left-1 border-${colorClass}  usa-alert usa-alert--${colorClass}`}
          data-testid="card-status-badge"
        >
          <Image
            src={`/icons/${icon}`}
            width={21}
            height={19}
            className="usa-icon margin-right-1 flex-shrink-0"
            alt=""
            aria-hidden="true"
          />
          {statusLabel}
        </div>
        {statusDescription && (
          <p className="margin-top-1 margin-bottom-0 text-base-dark font-body-xs">
            <RichText>{statusDescription}</RichText>
          </p>
        )}

        {/* Replacement link is rendered by ChildCard, not here */}
      </dd>
    </div>
  )
}
