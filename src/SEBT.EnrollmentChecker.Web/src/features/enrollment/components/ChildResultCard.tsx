'use client'

import { useTranslation } from 'react-i18next'
import type { DisplayStatus } from '../schemas/enrollmentSchema'

interface ChildResultCardProps {
  firstName: string
  lastName: string
  displayStatus: DisplayStatus
  errorMessage?: string | null
}

export function ChildResultCard({ firstName, lastName, displayStatus, errorMessage }: ChildResultCardProps) {
  const { t } = useTranslation('result')

  return (
    <div className="usa-card" data-status={displayStatus}>
      <div className="usa-card__body">
        <p>
          <strong>{firstName} {lastName}</strong>
          {/* Visually hidden status for screen readers */}
          <span className="usa-sr-only"> — {t(`status.${displayStatus}`)}</span>
        </p>
        {displayStatus === 'error' && errorMessage && (
          <p className="usa-prose text-error">{errorMessage}</p>
        )}
      </div>
    </div>
  )
}
