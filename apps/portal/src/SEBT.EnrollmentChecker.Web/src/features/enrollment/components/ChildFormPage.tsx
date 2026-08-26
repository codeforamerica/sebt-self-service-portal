'use client'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { getCheckerAssetPath } from '@/lib/checkerAssetPath'
import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useEnrollment, type Child } from '../context/EnrollmentContext'
import type { ChildFormValues } from '../schemas/childSchema'
import { ChildForm } from './ChildForm'

interface ChildFormPageProps {
  showSchoolField: boolean
  apiBaseUrl: string
  /**
   * Supplied by flows with no review step, which check straight from this form.
   * When absent the form hands off to the review screen instead.
   */
  onSubmitChildren?: (children: Child[]) => void
}

export function ChildFormPage({
  showSchoolField,
  apiBaseUrl,
  onSubmitChildren
}: ChildFormPageProps) {
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
  const formCard = getCheckerAssetPath('formCard')

  function handleSubmit(values: ChildFormValues) {
    // Editing only ever starts from the review screen, so it returns there.
    if (isEditMode && state.editingChildId) {
      updateChild(state.editingChildId, values)
      setEditingChildId(null)
      router.push('/review')
      return
    }

    const child = addChild(values)

    if (onSubmitChildren) {
      // Single-child flow: each check covers exactly the child just entered.
      // Passing the record directly also sidesteps the context update, which has
      // not landed yet on this pass.
      onSubmitChildren(child ? [child] : [])
      return
    }

    router.push('/review')
  }

  function handleCancel() {
    if (isEditMode) setEditingChildId(null)
    if (onSubmitChildren) {
      router.push('/')
      return
    }
    router.push(hasChildren ? '/review' : '/')
  }

  return (
    <div className="usa-section">
      <div className="grid-container">
        {formCard && (
          <Image
            src={formCard}
            alt=""
            width={100}
            height={75}
            aria-hidden="true"
          />
        )}
        <h1 className="font-family-sans margin-top-1 text-primary">{isEditMode ? t('editHeading', t('title')) : t('title')}</h1>
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
