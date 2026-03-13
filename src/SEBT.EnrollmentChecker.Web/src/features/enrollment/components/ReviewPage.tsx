'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'
import { useEnrollment } from '../context/EnrollmentContext'
import { ChildReviewCard } from './ChildReviewCard'

interface ReviewPageProps {
  onSubmit: () => void
}

export function ReviewPage({ onSubmit }: ReviewPageProps) {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const { state, removeChild, setEditingChildId } = useEnrollment()

  function handleEdit(id: string) {
    setEditingChildId(id)
    router.push('/check')
  }

  return (
    <div className="usa-section">
      <div className="grid-container">
        <button
          type="button"
          className="usa-button usa-button--unstyled margin-bottom-2"
          onClick={() => router.push('/check')}
        >
          {t('back', { ns: 'common' })}
        </button>
        <h1>{t('title')}</h1>
        {state.children.map(child => (
          <ChildReviewCard
            key={child.id}
            child={child}
            onEdit={handleEdit}
            onRemove={removeChild}
          />
        ))}
        <div className="usa-button-group margin-top-4">
          <Button variant="outline" onClick={() => router.push('/check')}>
            {t('actionAdd')}
          </Button>
          <Button onClick={onSubmit}>
            {t('submit', { ns: 'common' })}
          </Button>
        </div>
      </div>
    </div>
  )
}
