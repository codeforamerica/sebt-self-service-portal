'use client'

import { formatPersonName } from '@sebt/design-system'
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
    <section className="usa-summary-box__text" data-status={displayStatus}>
      <li>
        <strong>{formatPersonName(firstName)} {formatPersonName(lastName)}</strong>
        {/* Visually hidden status for screen readers */}
        <span className="usa-sr-only"> — {t(`status.${displayStatus}`)}</span>
      </li>
      {displayStatus === 'error' && errorMessage && (
        <p className="usa-prose text-error">{errorMessage}</p>
      )}
    </section>
  )
}
