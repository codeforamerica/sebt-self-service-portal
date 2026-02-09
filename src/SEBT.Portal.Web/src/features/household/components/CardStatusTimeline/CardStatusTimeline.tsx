'use client'

import { useTranslation } from 'react-i18next'

import type { Application, CardStatus } from '../../api'

interface CardStatusTimelineProps {
  application: Application
}

function formatDate(isoDate: string): string {
  const date = new Date(isoDate)
  return new Intl.DateTimeFormat('en-US', {
    month: '2-digit',
    day: '2-digit',
    year: 'numeric',
    timeZone: 'UTC'
  }).format(date)
}

type TimelineStep = {
  status: CardStatus
  label: string
  date: string | null
  isComplete: boolean
  isCurrent: boolean
}

function getTimelineSteps(application: Application): TimelineStep[] {
  const { cardStatus, cardRequestedAt, cardMailedAt, cardActivatedAt, cardDeactivatedAt } =
    application

  const statusOrder: CardStatus[] = ['Requested', 'Mailed', 'Active', 'Deactivated']
  const currentIndex = cardStatus ? statusOrder.indexOf(cardStatus) : -1

  // If deactivated, show full timeline including deactivation
  // Otherwise, only show steps up to Active
  const stepsToShow = cardStatus === 'Deactivated' ? statusOrder : statusOrder.slice(0, 3)

  return stepsToShow.map((status, index) => {
    let date: string | null = null
    switch (status) {
      case 'Requested':
        date = cardRequestedAt ?? null
        break
      case 'Mailed':
        date = cardMailedAt ?? null
        break
      case 'Active':
        date = cardActivatedAt ?? null
        break
      case 'Deactivated':
        date = cardDeactivatedAt ?? null
        break
    }

    return {
      status,
      label: status,
      date,
      isComplete: currentIndex >= index,
      isCurrent: currentIndex === index
    }
  })
}

function getStepClass(step: TimelineStep): string {
  if (step.status === 'Deactivated' && step.isComplete) {
    return 'usa-step-indicator__segment--complete bg-red'
  }
  if (step.isComplete) {
    return 'usa-step-indicator__segment--complete'
  }
  return ''
}

// Keys map to CSV: "S2 - Portal Dashboard - Card Table - Status {Status}"
// Note: CSV has full status strings like "Requested on [MM/DD/YYYY]" - we display date separately
// TODO: Add to CSV: "S2 - Portal Dashboard - Card Table - Status Aria Label" for timeline aria-label
// TODO: Add to CSV: "S2 - Portal Dashboard - Card Table - Status Not Complete" for sr-only text
export function CardStatusTimeline({ application }: CardStatusTimelineProps) {
  const { t } = useTranslation('dashboard')

  // Don't render timeline if no cardStatus or if status is Unknown
  if (!application.cardStatus || application.cardStatus === 'Unknown') {
    return null
  }

  const steps = getTimelineSteps(application)

  // Status label mapping to available locale keys
  const statusLabels: Record<CardStatus, string> = {
    Unknown: 'Unknown',
    Requested: 'Requested',
    Mailed: 'Mailed',
    Active: t('cardTableStatusActive'),
    Deactivated: t('cardTableStatusDeactivated')
  }

  return (
    <div className="margin-top-2">
      <dt className="text-bold">{t('cardTableHeadingCardStatus')}</dt>
      <dd className="margin-left-0 margin-top-1">
        <ol
          className="usa-step-indicator usa-step-indicator--counters-sm"
          aria-label={t('cardTableStatusAriaLabel', 'Card status timeline')}
        >
          {steps.map((step) => (
            <li
              key={step.status}
              className={`usa-step-indicator__segment ${getStepClass(step)}`}
              aria-current={step.isCurrent ? 'step' : undefined}
            >
              <span className="usa-step-indicator__segment-label">
                {statusLabels[step.status]}
                {step.date && (
                  <span className="display-block font-body-2xs text-base-dark">
                    {formatDate(step.date)}
                  </span>
                )}
                {!step.isComplete && (
                  <span className="usa-sr-only">
                    {t('cardTableStatusNotComplete', 'not complete')}
                  </span>
                )}
              </span>
            </li>
          ))}
        </ol>
      </dd>
    </div>
  )
}
