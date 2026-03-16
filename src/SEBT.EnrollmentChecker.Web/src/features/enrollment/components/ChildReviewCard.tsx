'use client'

import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'

interface ChildReviewCardProps {
  child: Child
  onEdit: (id: string) => void
}

/** Format ISO date (YYYY-MM-DD) as "Month, Day Year" (e.g., "April, 12 2015"). */
function formatBirthdate(dateOfBirth: string): string {
  const parts = dateOfBirth.split('-')
  const year = parts[0] ?? ''
  const month = parts[1] ?? ''
  const day = parts[2] ?? ''
  const monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ]
  const monthName = monthNames[parseInt(month, 10) - 1] ?? month
  return `${monthName}, ${parseInt(day, 10)} ${year}`
}

export function ChildReviewCard({ child, onEdit }: ChildReviewCardProps) {
  const { t } = useTranslation('confirmInfo')

  const middleInitial = child.middleName ? ` ${child.middleName.charAt(0)}.` : ''
  const fullName = `${child.firstName}${middleInitial} ${child.lastName}`

  return (
    <div className="usa-card">
      <div className="usa-card__body">
        <p className="usa-prose margin-bottom-05">
          <strong>{t('tableNameHeading')}</strong>
        </p>
        <p className="usa-prose margin-top-0">{fullName}</p>
        <p className="usa-prose margin-bottom-05">
          <strong>{t('tableBirthdateHeading')}</strong>
        </p>
        <p className="usa-prose margin-top-0">{formatBirthdate(child.dateOfBirth)}</p>
        <button
          type="button"
          className="usa-button usa-button--unstyled"
          onClick={() => onEdit(child.id)}
        >
          {t('tableAction')}
        </button>
      </div>
    </div>
  )
}
