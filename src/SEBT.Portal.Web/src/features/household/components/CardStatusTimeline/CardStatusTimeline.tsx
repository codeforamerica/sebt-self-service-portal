'use client'

import Image from 'next/image'
import { useTranslation } from 'react-i18next'

import { interpolateDate, type CardStatus } from '../../api'

interface CardStatusTimelineProps {
  cardStatus: CardStatus | null | undefined
  cardRequestedAt?: string | null | undefined
  cardMailedAt?: string | null | undefined
  cardDeactivatedAt?: string | null | undefined
}

type StepConfig = {
  borderClass: string
  bgClass: string
  icon: string
}

// Per-status color and icon from design
const STATUS_CONFIG: Partial<Record<CardStatus, StepConfig>> = {
  Requested: { borderClass: 'border-info', bgClass: 'bg-info-lighter', icon: 'credit_card_clock' },
  Mailed: { borderClass: 'border-info', bgClass: 'bg-info-lighter', icon: 'credit_card_check' },
  Processed: { borderClass: 'border-success', bgClass: 'bg-success-lighter', icon: 'mail_rounded' },
  Active: {
    borderClass: 'border-primary-light',
    bgClass: 'bg-primary-lightest',
    icon: 'credit_card_check'
  },
  Deactivated: { borderClass: 'border-base', bgClass: 'bg-base-lightest', icon: 'credit_card_off' }
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - {Key}"
export function CardStatusTimeline({
  cardStatus,
  cardRequestedAt,
  cardMailedAt,
  cardDeactivatedAt
}: CardStatusTimelineProps) {
  const { t, i18n } = useTranslation('dashboard')

  if (!cardStatus || cardStatus === 'Unknown') return null
  const config = STATUS_CONFIG[cardStatus]
  if (!config) return null

  // i18next returns '' (not the fallback arg) when a key exists with an empty value;
  // and the global parseMissingKeyHandler returns the key itself when no defaultValue
  // is supplied. Passing `{ defaultValue: '' }` collapses both cases to a falsy string
  // so the `|| jsFallback` chain reliably supplies English copy when the key is empty
  // or missing in the current state's locale.
  const statusLabels: Partial<Record<CardStatus, string>> = {
    Requested: t('cardTableStatusRequested', { defaultValue: '' }) || 'Requested on [MM/DD/YYYY]',
    Mailed: t('cardTableStatusIssued', { defaultValue: '' }) || 'Issued on [MM/DD/YYYY]',
    Processed: t('cardTableStatusMailed', { defaultValue: '' }) || 'Processed on [MM/DD/YYYY]',
    Active: t('cardTableStatusActive', { defaultValue: '' }) || 'Active',
    Deactivated: t('cardTableStatusDeactivated', { defaultValue: '' }) || 'Deactivated'
  }

  const statusDates: Partial<Record<CardStatus, string | null>> = {
    Requested: cardRequestedAt ?? null,
    Mailed: cardMailedAt ?? null,
    // TODO: No cardProcessedAt field in API — using cardMailedAt as best available date
    Processed: cardMailedAt ?? null,
    Active: null,
    Deactivated: cardDeactivatedAt ?? null
  }

  const rawLabel = statusLabels[cardStatus] ?? cardStatus
  const date = statusDates[cardStatus]
  const label = interpolateDate(rawLabel, date ?? null, i18n.language)

  return (
    <div className="margin-top-2">
      <dt className="text-bold">{t('cardTableHeadingCardStatus')}</dt>
      <dd className="margin-left-0 margin-top-1">
        <div
          className={`display-flex flex-align-center padding-1 border-left-1 ${config.borderClass} ${config.bgClass}`}
        >
          <Image
            src={`/icons/${config.icon}.svg`}
            width={21}
            height={19}
            className="usa-icon margin-right-1 flex-shrink-0"
            alt=""
            aria-hidden="true"
          />
          <span>{label}</span>
        </div>

        {/* TODO: Remove fallbacks once content team adds DC values to Google Sheet */}
        {/* TODO: Show cardTableStatusMessageRequested1 for new enrollee cards and
            cardTableStatusMessageRequested2 for replacement cards. Needs a backend
            field (e.g. replacementReason) to distinguish — no such field exists yet.
            Showing the new-enrollee message as the default for now. */}
        {cardStatus === 'Requested' && (
          <p className="margin-top-2 margin-bottom-0">
            {t('cardTableStatusMessageRequested1', { defaultValue: '' }) ||
              "We've requested a new DC SUN Bucks card that will arrive in the mail within 2–3 weeks. Check back here to see when the card has been mailed."}
          </p>
        )}
        {(cardStatus === 'Mailed' || cardStatus === 'Processed') && (
          <p className="margin-top-2 margin-bottom-0">
            {t('cardTableStatusMessageMailed', { defaultValue: '' }) ||
              "After the new card is mailed, it should arrive in around 5–7 days. If it doesn't arrive after two weeks, you can request a replacement card."}
          </p>
        )}
        {/* Replacement link is rendered by ChildCard, not here */}
        {/* TODO: Active and Deactivated status message fallbacks are placeholders —
            replace with real DC copy once content team updates the Google Sheet. */}
        {cardStatus === 'Active' && (
          <p className="margin-top-2 margin-bottom-0">
            {t('cardTableStatusMessageActive', { defaultValue: '' }) ||
              'Your card is active and ready to use.'}
          </p>
        )}
        {cardStatus === 'Deactivated' && (
          <p className="margin-top-2 margin-bottom-0">
            {t('cardTableStatusMessageDeactivated', { defaultValue: '' }) ||
              'This card has been deactivated.'}
          </p>
        )}
      </dd>
    </div>
  )
}
