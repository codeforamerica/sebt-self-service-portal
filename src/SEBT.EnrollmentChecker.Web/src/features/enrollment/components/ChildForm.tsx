'use client'

import { InputField } from '@sebt/design-system'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'
import type { ChildFormValues } from '../schemas/childSchema'
import { childSchema } from '../schemas/childSchema'
import { SchoolSelect } from './SchoolSelect'

interface ChildFormProps {
  initialValues?: Child
  onSubmit: (values: ChildFormValues) => void
  onCancel?: () => void
  showSchoolField: boolean
  apiBaseUrl: string
}

export function ChildForm({
  initialValues,
  onSubmit,
  onCancel,
  showSchoolField,
  apiBaseUrl
}: ChildFormProps) {
  const { t } = useTranslation('personalInfo')
  const [values, setValues] = useState<Partial<ChildFormValues>>({
    firstName: initialValues?.firstName ?? '',
    middleName: initialValues?.middleName ?? '',
    lastName: initialValues?.lastName ?? '',
    dateOfBirth: initialValues?.dateOfBirth ?? '',
    schoolName: initialValues?.schoolName,
    schoolCode: initialValues?.schoolCode
  })
  const [errors, setErrors] = useState<Partial<Record<keyof ChildFormValues, string>>>({})

  function set(field: keyof ChildFormValues, value: string) {
    setValues(v => ({ ...v, [field]: value }))
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const result = childSchema.safeParse(values)
    if (!result.success) {
      const fieldErrors: Partial<Record<keyof ChildFormValues, string>> = {}
      for (const issue of result.error.issues) {
        const key = issue.path[0] as keyof ChildFormValues
        if (!fieldErrors[key]) fieldErrors[key] = issue.message
      }
      setErrors(fieldErrors)
      return
    }
    setErrors({})
    onSubmit(result.data)
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <InputField
        label={t('firstNameLabel')}
        value={values.firstName ?? ''}
        onChange={e => set('firstName', e.target.value)}
        error={errors.firstName}
        isRequired
      />
      <InputField
        label={t('middleNameLabel')}
        value={values.middleName ?? ''}
        onChange={e => set('middleName', e.target.value)}
      />
      <InputField
        label={t('lastNameLabel')}
        value={values.lastName ?? ''}
        onChange={e => set('lastName', e.target.value)}
        error={errors.lastName}
        isRequired
      />
      <InputField
        label={t('dobLabel')}
        value={values.dateOfBirth ?? ''}
        onChange={e => set('dateOfBirth', e.target.value)}
        error={errors.dateOfBirth}
        isRequired
        hint={t('dobHint')}
      />
      <SchoolSelect
        enabled={showSchoolField}
        apiBaseUrl={apiBaseUrl}
        value={values.schoolCode ?? ''}
        onChange={(code, name) => {
          set('schoolCode', code)
          set('schoolName', name)
        }}
      />
      <div className="usa-button-group margin-top-4">
        {onCancel && (
          <button type="button" className="usa-button usa-button--outline" onClick={onCancel}>
            {t('cancel', { ns: 'common' })}
          </button>
        )}
        <button type="submit" className="usa-button">
          {t('continue', { ns: 'common' })}
        </button>
      </div>
    </form>
  )
}
