'use client'

import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'

interface ChildReviewCardProps {
  child: Child
  onEdit: (id: string) => void
  onRemove: (id: string) => void
}

/** Format ISO date (YYYY-MM-DD) as a locale-aware date string (e.g., "April 12, 2015"). */
function formatBirthdate(dateOfBirth: string, locale: string): string {
  const parts = dateOfBirth.split('-').map(Number)
  const date = new Date(parts[0]!, parts[1]! - 1, parts[2]!)
  return date.toLocaleDateString(locale, { year: 'numeric', month: 'long', day: 'numeric' })
}

export function ChildReviewCard({ child, onEdit, onRemove }: ChildReviewCardProps) {
  const { t, i18n } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')

  const middleInitial = child.middleName ? ` ${child.middleName.charAt(0)}.` : ''
  const fullName = `${child.firstName}${middleInitial} ${child.lastName}`

  return (
    <div className="child-review-card">
      <p className="usa-prose margin-bottom-05">
        <strong>{t('tableNameHeading')}</strong>
      </p>
      <p className="usa-prose margin-top-0">{fullName}</p>
      <p className="usa-prose margin-bottom-05">
        <strong>{t('tableBirthdateHeading')}</strong>
      </p>
      <p className="usa-prose margin-top-0">{formatBirthdate(child.dateOfBirth, i18n.language)}</p>
      <div className="display-flex flex-row flex-wrap">
        <button
          type="button"
          className="usa-link usa-button--unstyled margin-right-2"
          aria-label={`${t('tableAction')}: ${fullName}`}
          onClick={() => onEdit(child.id)}
        >
          {t('tableAction')}
        </button>
        <button
          type="button"
          className="usa-link usa-button--unstyled text-error-dark"
          aria-label={`${tCommon('remove')}: ${fullName}`}
          onClick={() => onRemove(child.id)}
        >
          {tCommon('remove')}
        </button>
      </div>
    </div>
  )
}
