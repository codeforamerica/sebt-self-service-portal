'use client'

import { useTranslation } from 'react-i18next'

import type { Application, CardStatus, UiCardStatus } from '../../api'
import { toUiCardStatus } from '../../api'

interface CardStatusDisplayProps {
  application: Application
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - Status {Status}"
const STATUS_CONFIG: Record<UiCardStatus, { colorClass: string; labelKey: string }> = {
  Active: { colorClass: 'bg-success-dark text-white', labelKey: 'cardTableStatusActive' },
  Processed: { colorClass: 'bg-info-dark text-white', labelKey: 'cardTableStatusProcessed' },
  Inactive: { colorClass: 'bg-error-dark text-white', labelKey: 'cardTableStatusInactive' },
  Frozen: { colorClass: 'bg-warning-dark text-white', labelKey: 'cardTableStatusFrozen' },
  Undeliverable: {
    colorClass: 'bg-error-dark text-white',
    labelKey: 'cardTableStatusUndeliverable'
  }
}

const DESCRIPTION_KEY: Partial<Record<CardStatus, string>> = {
  Active: 'cardTableStatusMessageActive',
  Processed: 'cardTableStatusMessageProcessed',
  Lost: 'cardTableStatusMessageInactive',
  Stolen: 'cardTableStatusMessageInactive',
  Damaged: 'cardTableStatusMessageInactive',
  Deactivated: 'cardTableStatusMessageDeactivated',
  DeactivatedByState: 'cardTableStatusMessageDeactivated',
  NotActivated: 'cardTableStatusMessageDeactivated',
  Frozen: 'cardTableStatusMessageFrozen',
  Undeliverable: 'cardTableStatusMessageUndeliverable'
}

export function CardStatusDisplay({ application }: CardStatusDisplayProps) {
  const { t } = useTranslation('dashboard')

  const { cardStatus } = application

  if (
    !cardStatus ||
    cardStatus === 'Unknown' ||
    cardStatus === 'Requested' ||
    cardStatus === 'Mailed'
  )
    return null

  const uiStatus = toUiCardStatus(cardStatus)
  const { colorClass, labelKey } = STATUS_CONFIG[uiStatus]
  const statusLabel = t(labelKey)
  const statusDescription = t(DESCRIPTION_KEY[cardStatus] ?? 'cardTableStatusMessageInactive')

  return (
    <div className="margin-top-2">
      <dt className="text-bold">{t('cardTableHeadingCardStatus')}</dt>
      <dd className="margin-left-0 margin-top-1">
        <div className="border-1px border-base-lighter radius-md padding-2">
          <span
            className={`usa-tag ${colorClass} radius-pill padding-x-2 padding-y-05`}
            data-testid="card-status-badge"
          >
            {statusLabel}
          </span>

          <p className="margin-top-1 margin-bottom-0 text-base-dark font-body-xs">
            {statusDescription}
          </p>

          {/* Replacement link is rendered by ChildCard, not here */}
        </div>
      </dd>
    </div>
  )
}
