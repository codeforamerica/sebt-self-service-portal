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

// This page carries no busy state of its own: while the enrollment check is in
// flight, the review/page.tsx container replaces it wholesale with the
// LoadingInterstitial, so button-level loading props here would never render.
export function ReviewPage({ onSubmit }: ReviewPageProps) {
  // confirmInfo title/body exist only in the CO sheet (DC marks them !N/A!);
  // a DC build renders raw key names on this screen.
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { state, setEditingChildId, removeChild } = useEnrollment()

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
        <h1 className="font-family-sans margin-top-1 text-primary">{t('title')}</h1>
        <p className="usa-prose">{t('body')}</p>

        <div className="margin-top-3">
          {state.children.map((child) => (
            <ChildReviewCard
              key={child.id}
              child={child}
              onEdit={handleEdit}
              onRemove={removeChild}
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
          <Button
            onClick={onSubmit}
            disabled={state.children.length === 0}
          >
            {tCommon('submit')}
          </Button>
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
