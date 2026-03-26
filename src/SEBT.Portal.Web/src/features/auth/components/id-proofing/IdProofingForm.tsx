'use client'

import { useRouter } from 'next/navigation'
import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { Alert, Button, InputField } from '@sebt/design-system'

import { SK_CHALLENGE_ID } from '@/features/auth/components/doc-verify/sessionKeys'
import { useSubmitIdProofing, type IdType } from '../../api'

// UI-only sentinel value for the "none" radio option.
// The API receives idType: null when the user selects this.
const NONE_VALUE = 'none' as const

type IdOptionValue = IdType | typeof NONE_VALUE

export interface IdOption {
  value: IdOptionValue
  /** i18next key for the radio label */
  labelKey: string
  /** i18next key for the helper text below the radio label (optional) */
  helperKey?: string
  /** i18next key for the text input label shown when this option is selected */
  inputLabelKey?: string
}

interface IdProofingFormProps {
  idOptions: IdOption[]
  contactLink: string
}

// Generate localized month names using Intl.DateTimeFormat
function getLocalizedMonths(locale: string) {
  const formatter = new Intl.DateTimeFormat(locale, { month: 'long' })
  return Array.from({ length: 12 }, (_, i) => ({
    value: String(i + 1).padStart(2, '0'),
    label: formatter.format(new Date(2024, i, 1))
  }))
}

export function IdProofingForm({ idOptions, contactLink }: IdProofingFormProps) {
  const router = useRouter()
  const { t, i18n } = useTranslation('idProofing')
  const { t: tCommon } = useTranslation('common')
  const { t: tPersonalInfo } = useTranslation('personalInfo')
  const { t: tValidation } = useTranslation('validation')
  const formId = useId()
  const months = getLocalizedMonths(i18n.language)

  const [dobMonth, setDobMonth] = useState('')
  const [dobDay, setDobDay] = useState('')
  const [dobYear, setDobYear] = useState('')
  const [selectedIdType, setSelectedIdType] = useState<IdOptionValue | null>(null)
  const [idValue, setIdValue] = useState('')

  const [dobErrors, setDobErrors] = useState<{ month?: string; day?: string; year?: string }>({})
  const [idTypeError, setIdTypeError] = useState<string | null>(null)
  const [idValueError, setIdValueError] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const submitIdProofing = useSubmitIdProofing()
  const isSubmitting = submitIdProofing.isPending

  const selectedOption = idOptions.find((opt) => opt.value === selectedIdType)
  const showIdValueInput = selectedIdType !== null && selectedIdType !== NONE_VALUE

  const REQUIRED_FIELD_ERROR = tValidation('required')

  function validateFields(): boolean {
    const newDobErrors: { month?: string; day?: string; year?: string } = {}

    if (!dobMonth) newDobErrors.month = REQUIRED_FIELD_ERROR
    if (!dobDay) newDobErrors.day = REQUIRED_FIELD_ERROR
    if (!dobYear) newDobErrors.year = REQUIRED_FIELD_ERROR

    setDobErrors(newDobErrors)

    let idTypeErr: string | null = null
    if (selectedIdType === null) {
      idTypeErr = REQUIRED_FIELD_ERROR
    }
    setIdTypeError(idTypeErr)

    let idError: string | null = null
    if (showIdValueInput && !idValue.trim()) {
      idError = REQUIRED_FIELD_ERROR
    }
    setIdValueError(idError)

    return Object.keys(newDobErrors).length === 0 && idTypeErr === null && idError === null
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitError(null)

    if (!validateFields()) return

    try {
      const response = await submitIdProofing.mutateAsync({
        dateOfBirth: { month: dobMonth, day: dobDay, year: dobYear },
        // Map the UI "none" sentinel to null for the API
        idType: selectedIdType === NONE_VALUE || selectedIdType === null ? null : selectedIdType,
        idValue: showIdValueInput ? idValue.trim() : null
      })

      if (response.result === 'documentVerificationRequired') {
        if (!response.challengeId) {
          setSubmitError(
            t('idProofingStartError', 'Unable to start document verification. Please try again.')
          )
          return
        }
        sessionStorage.setItem(SK_CHALLENGE_ID, response.challengeId)
        router.push(`/login/id-proofing/doc-verify?challengeId=${response.challengeId}`)
      } else if (response.result === 'failed') {
        const params = new URLSearchParams()
        if (response.canApply === false) {
          params.set('canApply', 'false')
        }
        const query = params.toString()
        router.push(`/login/id-proofing/off-boarding${query ? `?${query}` : ''}`)
      } else {
        router.push('/dashboard')
      }
    } catch (err) {
      // All errors get the same user-facing message. Raw ApiError.message may contain
      // backend wording not intended for end users — avoid displaying it directly.
      void err
      setSubmitError(t('idProofingGenericError', 'Something went wrong. Please try again.'))
    }
  }

  return (
    <form
      className="usa-form maxw-full text-left"
      onSubmit={handleSubmit}
    >
      {submitError && (
        <Alert
          variant="error"
          slim
          className="margin-bottom-2"
        >
          {submitError}
        </Alert>
      )}

      {/* Date of birth */}
      <fieldset className="usa-fieldset">
        <legend className="usa-legend">
          {t('labelDob')}
          <span className="text-secondary-dark"> *</span>
        </legend>

        <div className="grid-row grid-gap">
          {/* Month */}
          <div className="mobile-lg:grid-col-4">
            <div
              className={
                dobErrors.month ? 'usa-form-group usa-form-group--error' : 'usa-form-group'
              }
            >
              <label
                className="usa-label"
                htmlFor={`${formId}-dob-month`}
              >
                {tPersonalInfo('labelMonth')}
              </label>
              {dobErrors.month && (
                <span
                  className="usa-error-message"
                  role="alert"
                >
                  {dobErrors.month}
                </span>
              )}
              <select
                id={`${formId}-dob-month`}
                className={`usa-select${dobErrors.month ? ' usa-input--error' : ''}`}
                value={dobMonth}
                onChange={(e) => setDobMonth(e.target.value)}
                autoComplete="bday-month"
                aria-required="true"
                aria-invalid={!!dobErrors.month}
              >
                <option value=""></option>
                {months.map((m) => (
                  <option
                    key={m.value}
                    value={m.value}
                  >
                    {m.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Day */}
          <div className="mobile-lg:grid-col-4">
            <InputField
              label={tPersonalInfo('labelDay')}
              type="text"
              inputMode="numeric"
              name="dobDay"
              maxLength={2}
              value={dobDay}
              onChange={(e) => setDobDay(e.target.value)}
              autoComplete="bday-day"
              isRequired
              {...(dobErrors.day ? { error: dobErrors.day } : {})}
            />
          </div>

          {/* Year */}
          <div className="mobile-lg:grid-col-4">
            <InputField
              label={tPersonalInfo('labelYear')}
              type="text"
              inputMode="numeric"
              name="dobYear"
              maxLength={4}
              value={dobYear}
              onChange={(e) => setDobYear(e.target.value)}
              autoComplete="bday-year"
              isRequired
              {...(dobErrors.year ? { error: dobErrors.year } : {})}
            />
          </div>
        </div>
      </fieldset>

      {/* ID type selection */}
      <fieldset className="usa-fieldset margin-top-3">
        <legend className="usa-legend">
          {t('labelId')}
          <span className="text-secondary-dark"> *</span>
        </legend>

        {idTypeError && (
          <span
            className="usa-error-message"
            role="alert"
          >
            {idTypeError}
          </span>
        )}

        {idOptions.map((option) => (
          <div
            key={option.value}
            className="usa-radio"
          >
            <input
              className="usa-radio__input usa-radio__input--tile"
              type="radio"
              id={`${formId}-id-type-${option.value}`}
              name="idType"
              value={option.value}
              checked={selectedIdType === option.value}
              onChange={() => {
                setSelectedIdType(option.value)
                setIdValue('')
                setIdTypeError(null)
                setIdValueError(null)
              }}
            />
            <label
              className="usa-radio__label"
              htmlFor={`${formId}-id-type-${option.value}`}
            >
              {t(option.labelKey)}
              {option.helperKey && (
                <span className="usa-radio__label-description">{t(option.helperKey)}</span>
              )}
            </label>
          </div>
        ))}
      </fieldset>

      {/* Conditional ID value input */}
      {showIdValueInput && selectedOption?.inputLabelKey && (
        <div className="margin-top-2">
          <InputField
            label={t(selectedOption.inputLabelKey)}
            type="text"
            name="idValue"
            value={idValue}
            onChange={(e) => setIdValue(e.target.value)}
            autoComplete="off"
            isRequired
            {...(idValueError ? { error: idValueError } : {})}
          />
        </div>
      )}

      <Button
        type="submit"
        isLoading={isSubmitting}
        loadingText={`${tCommon('continue')}...`}
        className="margin-top-3 display-block"
        disabled={isSubmitting}
      >
        {tCommon('continue')}
      </Button>

      <p className="margin-top-4 font-sans-sm">
        <a
          href={contactLink}
          target="_blank"
          rel="noopener noreferrer"
          className="usa-link"
        >
          {tCommon('linkContactUs')}
        </a>
      </p>
    </form>
  )
}
