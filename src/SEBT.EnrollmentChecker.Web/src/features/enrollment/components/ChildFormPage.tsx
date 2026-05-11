'use client'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useEnrollment } from '../context/EnrollmentContext'
import type { ChildFormValues } from '../schemas/childSchema'
import { ChildForm } from './ChildForm'

interface ChildFormPageProps {
  showSchoolField: boolean
  apiBaseUrl: string
}

export function ChildFormPage({ showSchoolField, apiBaseUrl }: ChildFormPageProps) {
  const { t } = useTranslation('personalInfo')
  const router = useRouter()
  const { state, addChild, updateChild, setEditingChildId } = useEnrollment()
  const { trackEvent } = useDataLayer()

  useEffect(() => {
    trackEvent(AnalyticsEvents.ENROLLMENT_CHECK_START)
  }, [trackEvent])

  const editingChild = state.editingChildId
    ? state.children.find(c => c.id === state.editingChildId)
    : undefined

  const isEditMode = !!editingChild
  const hasChildren = state.children.length > 0

  function handleSubmit(values: ChildFormValues) {
    if (isEditMode && state.editingChildId) {
      updateChild(state.editingChildId, values)
      setEditingChildId(null)
    } else {
      addChild(values)
    }
    router.push('/review')
  }

  function handleCancel() {
    if (isEditMode) setEditingChildId(null)
    router.push(hasChildren ? '/review' : '/')
  }

  return (
    <div className="usa-section">
      <div className="grid-container">
        <Image
          src={`${process.env.NEXT_PUBLIC_BASE_PATH}/images/states/co/icon-form-card.svg`}
          alt=""
          width={100}
          height={75}
          aria-hidden="true"
        />
        {/* added temp fallback */}
        <h1 className="font-family-sans margin-top-1">{isEditMode ? t('editHeading', t('title')) : t('title')}</h1>
        <p className="usa-prose">{t('body')}</p>
        <p className="usa-hint">{t('requiredFields', { ns: 'common' })}</p>
        <ChildForm
          {...(editingChild && { initialValues: editingChild })}
          onSubmit={handleSubmit}
          onCancel={handleCancel}
          showSchoolField={showSchoolField}
          apiBaseUrl={apiBaseUrl}
        />
      </div>
    </div>
  )
}
