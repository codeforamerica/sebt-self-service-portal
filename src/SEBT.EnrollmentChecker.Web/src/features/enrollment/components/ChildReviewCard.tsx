'use client'

import { Button } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'

interface ChildReviewCardProps {
  child: Child
  onEdit: (id: string) => void
  onRemove: (id: string) => void
}

export function ChildReviewCard({ child, onEdit, onRemove }: ChildReviewCardProps) {
  const { t } = useTranslation('common')

  return (
    <div className="usa-card">
      <div className="usa-card__body">
        <p className="usa-prose">
          <strong>{child.firstName} {child.lastName}</strong>
          {' — '}{child.dateOfBirth}
        </p>
        <div className="usa-button-group">
          <Button variant="unstyled" onClick={() => onEdit(child.id)}>
            {t('editChild')}
          </Button>
          <Button variant="unstyled" onClick={() => onRemove(child.id)}>
            {t('removeChild')}
          </Button>
        </div>
      </div>
    </div>
  )
}
