'use client'

import { Button } from '@sebt/design-system'
import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'
import { useEnrollment } from '../context/EnrollmentContext'
import { ChildReviewCard } from './ChildReviewCard'

interface ReviewPageProps {
  onSubmit: () => void
}

export function ReviewPage({ onSubmit }: ReviewPageProps) {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { state, setEditingChildId } = useEnrollment()

  function handleEdit(id: string) {
    setEditingChildId(id)
    router.push('/check')
  }

  return (
    <div className="usa-section">
      <div className="grid-container">
        <Image
          src={`${process.env.NEXT_PUBLIC_BASE_PATH}/images/states/co/icon-review-card.svg`}
          alt=""
          width={100}
          height={75}
          aria-hidden="true"
        />
        <h1 className="font-family-sans margin-top-1">{t('title')}</h1>
        <p className="usa-prose">{t('body')}</p>

        <div className="margin-top-3">
          {state.children.map((child) => (
            <ChildReviewCard
              key={child.id}
              child={child}
              onEdit={handleEdit}
            />
          ))}
        </div>

        <div className="display-flex flex-row flex-align-center margin-top-4">
          <Button
            variant="outline"
            className="margin-right-1"
            onClick={() => router.push('/check')}
          >
            {tCommon('back')}
          </Button>
          <Button onClick={onSubmit}>{tCommon('submit')}</Button>
        </div>
        <div className="margin-top-2">
          <button
            type="button"
            className="usa-link usa-button--unstyled"
            onClick={() => {
              setEditingChildId(null)
              router.push('/check')
            }}
          >
            {t('actionAdd')}
          </button>
        </div>
      </div>
    </div>
  )
}
