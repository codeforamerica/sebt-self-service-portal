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

const MONTH_KEYS = [
  'january', 'february', 'march', 'april', 'may', 'june',
  'july', 'august', 'september', 'october', 'november', 'december'
] as const

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
        // Zod messages are i18n keys — resolve them to translated strings
        if (!fieldErrors[key]) fieldErrors[key] = tCommon(issue.message)
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
            {errors.month && <span id="date-month-error" className="usa-error-message" role="alert">{errors.month}</span>}
            <select
              className={`usa-select${errors.month ? ' usa-input--error' : ''}`}
              id="date-month"
              name="month"
              aria-label={t('labelMonth')}
              aria-invalid={!!errors.month}
              aria-describedby={errors.month ? 'date-month-error' : undefined}
              value={values.month ?? ''}
              onChange={e => set('month', e.target.value)}
            >
              <option value="">{tCommon('selectOne')}</option>
              {MONTH_KEYS.map((key, i) => (
                <option key={i + 1} value={String(i + 1)}>{tCommon(`months.${key}`)}</option>
              ))}
            </select>
          </div>
          <div className="usa-form-group usa-form-group--day">
            <label className="usa-label" htmlFor="date-day">{t('labelDay')}</label>
            {errors.day && <span id="date-day-error" className="usa-error-message" role="alert">{errors.day}</span>}
            <input
              className={`usa-input usa-input--inline${errors.day ? ' usa-input--error' : ''}`}
              id="date-day"
              name="day"
              type="text"
              inputMode="numeric"
              maxLength={2}
              aria-label={t('labelDay')}
              aria-invalid={!!errors.day}
              aria-describedby={errors.day ? 'date-day-error' : undefined}
              value={values.day ?? ''}
              onChange={e => set('day', e.target.value)}
            />
          </div>
          <div className="usa-form-group usa-form-group--year">
            <label className="usa-label" htmlFor="date-year">{t('labelYear')}</label>
            {errors.year && <span id="date-year-error" className="usa-error-message" role="alert">{errors.year}</span>}
            <input
              className={`usa-input usa-input--inline${errors.year ? ' usa-input--error' : ''}`}
              id="date-year"
              name="year"
              type="text"
              inputMode="numeric"
              maxLength={4}
              aria-label={t('labelYear')}
              aria-invalid={!!errors.year}
              aria-describedby={errors.year ? 'date-year-error' : undefined}
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
      <div className="display-flex flex-row flex-align-center margin-top-4">
        {onCancel && (
          <button type="button" className="usa-button usa-button--outline margin-right-1" onClick={onCancel}>
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
