'use client'

import { InputField } from '@sebt/design-system'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Child } from '../context/EnrollmentContext'
import type { ChildFormValues } from '../schemas/childSchema'
import { childFormSchema, fromDateOfBirth } from '../schemas/childSchema'
import { SchoolSelect } from './SchoolSelect'

interface ChildFormProps {
  initialValues?: Child
  onSubmit: (values: ChildFormValues) => void
  onCancel?: () => void
  showSchoolField: boolean
  apiBaseUrl: string
}

const MONTHS = [
  { value: '1', label: 'January' },
  { value: '2', label: 'February' },
  { value: '3', label: 'March' },
  { value: '4', label: 'April' },
  { value: '5', label: 'May' },
  { value: '6', label: 'June' },
  { value: '7', label: 'July' },
  { value: '8', label: 'August' },
  { value: '9', label: 'September' },
  { value: '10', label: 'October' },
  { value: '11', label: 'November' },
  { value: '12', label: 'December' },
]

export function ChildForm({
  initialValues,
  onSubmit,
  onCancel,
  showSchoolField,
  apiBaseUrl
}: ChildFormProps) {
  const { t } = useTranslation('personalInfo')
  const { t: tCommon } = useTranslation('common')

  // If editing, decompose the stored dateOfBirth into month/day/year
  const initialDate = initialValues?.dateOfBirth
    ? fromDateOfBirth(initialValues.dateOfBirth)
    : { month: '', day: '', year: '' }

  const [values, setValues] = useState<Partial<ChildFormValues>>({
    firstName: initialValues?.firstName ?? '',
    middleName: initialValues?.middleName ?? '',
    lastName: initialValues?.lastName ?? '',
    month: initialDate.month,
    day: initialDate.day,
    year: initialDate.year,
    schoolName: initialValues?.schoolName,
    schoolCode: initialValues?.schoolCode
  })
  const [errors, setErrors] = useState<Partial<Record<keyof ChildFormValues, string>>>({})

  function set(field: keyof ChildFormValues, value: string) {
    setValues(v => ({ ...v, [field]: value }))
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const result = childFormSchema.safeParse(values)
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

  const nameHint = tCommon('legallyAsItAppears')

  return (
    <form onSubmit={handleSubmit} noValidate>
      <InputField
        label={tCommon('labelFirstName')}
        value={values.firstName ?? ''}
        onChange={e => set('firstName', e.target.value)}
        error={errors.firstName}
        isRequired
        hint={nameHint}
      />
      <InputField
        label={tCommon('labelMiddleName')}
        value={values.middleName ?? ''}
        onChange={e => set('middleName', e.target.value)}
        hint={tCommon('optional')}
      />
      <InputField
        label={tCommon('labelLastName')}
        value={values.lastName ?? ''}
        onChange={e => set('lastName', e.target.value)}
        error={errors.lastName}
        isRequired
        hint={nameHint}
      />

      {/* USWDS memorable-date pattern: Month dropdown + Day/Year text inputs */}
      <fieldset className="usa-fieldset">
        <legend className="usa-legend">
          {t('labelBirthdate')} <abbr title="required" className="usa-hint usa-hint--required">*</abbr>
        </legend>
        <div className="usa-memorable-date">
          <div className="usa-form-group usa-form-group--month">
            <label className="usa-label" htmlFor="date-month">{t('labelMonth')}</label>
            {errors.month && <span className="usa-error-message">{errors.month}</span>}
            <select
              className={`usa-select${errors.month ? ' usa-input--error' : ''}`}
              id="date-month"
              name="month"
              aria-label={t('labelMonth')}
              value={values.month ?? ''}
              onChange={e => set('month', e.target.value)}
            >
              <option value="">{tCommon('selectOne')}</option>
              {MONTHS.map(m => (
                <option key={m.value} value={m.value}>{m.label}</option>
              ))}
            </select>
          </div>
          <div className="usa-form-group usa-form-group--day">
            <label className="usa-label" htmlFor="date-day">{t('labelDay')}</label>
            {errors.day && <span className="usa-error-message">{errors.day}</span>}
            <input
              className={`usa-input usa-input--inline${errors.day ? ' usa-input--error' : ''}`}
              id="date-day"
              name="day"
              type="text"
              inputMode="numeric"
              maxLength={2}
              aria-label={t('labelDay')}
              value={values.day ?? ''}
              onChange={e => set('day', e.target.value)}
            />
          </div>
          <div className="usa-form-group usa-form-group--year">
            <label className="usa-label" htmlFor="date-year">{t('labelYear')}</label>
            {errors.year && <span className="usa-error-message">{errors.year}</span>}
            <input
              className={`usa-input usa-input--inline${errors.year ? ' usa-input--error' : ''}`}
              id="date-year"
              name="year"
              type="text"
              inputMode="numeric"
              maxLength={4}
              aria-label={t('labelYear')}
              value={values.year ?? ''}
              onChange={e => set('year', e.target.value)}
            />
          </div>
        </div>
      </fieldset>

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
            {tCommon('back')}
          </button>
        )}
        <button type="submit" className="usa-button">
          {tCommon('continue')}
        </button>
      </div>
    </form>
  )
}
