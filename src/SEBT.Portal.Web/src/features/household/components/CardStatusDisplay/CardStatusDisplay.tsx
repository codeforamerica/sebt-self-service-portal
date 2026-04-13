'use client'

import { useTranslation } from 'react-i18next'

import type { CardStatus, UiCardStatus } from '../../api'
import { toUiCardStatus } from '../../api'

interface CardStatusDisplayProps {
  cardStatus: CardStatus | null | undefined
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

// Fallback English copy used when the generated locale string is missing or empty
// (the DC column in dc.csv has empty EN cells for several card-status messages).
// TODO: Remove once dc.csv EN cells are filled in for cardTableStatusMessage* keys.
const DESCRIPTION_FALLBACK: Partial<Record<CardStatus, string>> = {
  Active:
    "This card has been sent to you. It is ready to make purchases as soon as you set the PIN. When the card arrives, call EBT Customer Service at (888) 328-2656 to set the card's PIN.",
  Processed: 'Your card has been processed and is on its way.',
  Lost: 'This card was reported as lost, stolen, or damaged. Request a replacement card above.',
  Stolen: 'This card was reported as lost, stolen, or damaged. Request a replacement card above.',
  Damaged: 'This card was reported as lost, stolen, or damaged. Request a replacement card above.',
  Deactivated:
    'This card was reported as lost, stolen, damaged, or otherwise inactive. Contact customer service for help.',
  DeactivatedByState: 'This card was deactivated by the state. Contact customer service for help.',
  NotActivated: 'This card has not been activated. Contact customer service for help.',
  Frozen: 'This card is frozen. Contact customer service for help.',
  Undeliverable:
    'This card could not be delivered. Verify your address is correct and contact customer service.'
}

export function CardStatusDisplay({ cardStatus }: CardStatusDisplayProps) {
  const { t } = useTranslation('dashboard')

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
  const descriptionKey = DESCRIPTION_KEY[cardStatus] ?? 'cardTableStatusMessageInactive'
  const translated = t(descriptionKey)
  // Empty-string locale entries are treated as missing so the English fallback surfaces.
  const statusDescription = translated || DESCRIPTION_FALLBACK[cardStatus] || ''

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
