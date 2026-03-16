'use client'

import Image from 'next/image'
import { useRouter } from 'next/navigation'
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
        <button
          type="button"
          className="usa-button usa-button--unstyled margin-bottom-2"
          onClick={handleCancel}
        >
          {t('back', { ns: 'common' })}
        </button>
        <Image
          src="/img/icon-form-card.svg"
          alt=""
          width={100}
          height={75}
          aria-hidden="true"
        />
        <h1 className="font-family-sans margin-top-1">{isEditMode ? t('editHeading') : t('title')}</h1>
        <p className="usa-prose">{t('body')}</p>
        <p className="usa-hint">{t('requiredFields', { ns: 'common' })}</p>
        <ChildForm
          initialValues={editingChild}
          onSubmit={handleSubmit}
          onCancel={handleCancel}
          showSchoolField={showSchoolField}
          apiBaseUrl={apiBaseUrl}
        />
      </div>
    </div>
  )
}
