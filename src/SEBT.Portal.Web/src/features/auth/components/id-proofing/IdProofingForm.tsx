'use client'

import { useRouter } from 'next/navigation'
import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert, Button, InputField } from '@sebt/design-system'

import {
  clearChallengeContext,
  SK_CHALLENGE_ID
} from '@/features/auth/components/doc-verify/sessionKeys'
import { SubmitIdProofingRequestSchema, useSubmitIdProofing, type IdType } from '../../api'

// UI-only sentinel value for the "none" radio option.
// The API receives idType: null when the user selects this.
const NONE_VALUE = 'none' as const

type IdOptionValue = IdType | typeof NONE_VALUE

/**
 * Per-option validation rule for the ID value input.
 *
 * `digits: 9` means exactly 9 digits (after non-digit stripping).
 * `digits: [7, 8]` means inclusive range.
 *
 * Undefined/absent means no digit-count check. The input renders as a plain
 * text field and only the base "required" rule applies.
 */
export interface IdOptionValidation {
  digits: number | [number, number]
}

export interface IdOption {
  value: IdOptionValue
  /** i18next key for the radio label */
  labelKey: string
  /** i18next key for the helper text below the radio label (optional) */
  helperKey?: string
  /** i18next key for the text input label shown when this option is selected */
  inputLabelKey?: string
  /**
   * Digit-count rule for the associated ID value input. When present, the form
   * strips non-digits on change, applies a numeric keypad, caps length at the
   * rule's upper bound, and enforces the rule on submit. State-specific rules
   * live on the option rather than in the shared Zod schema.
   */
  validation?: IdOptionValidation
}

// Returns [min, max] for either form of the digits rule.
function digitBounds(rule: IdOptionValidation): [number, number] {
  return Array.isArray(rule.digits) ? rule.digits : [rule.digits, rule.digits]
}

function matchesDigitRule(value: string, rule: IdOptionValidation): boolean {
  const digits = value.replace(/\D/g, '')
  const [min, max] = digitBounds(rule)
  return digits.length >= min && digits.length <= max
}

interface IdProofingFormProps {
  idOptions: IdOption[]
  contactLink: string
  getDiToken?: () => Promise<string | null>
}

// Generate localized month names using Intl.DateTimeFormat
function getLocalizedMonths(locale: string) {
  const formatter = new Intl.DateTimeFormat(locale, { month: 'long' })
  return Array.from({ length: 12 }, (_, i) => ({
    value: String(i + 1).padStart(2, '0'),
    label: formatter.format(new Date(2024, i, 1))
  }))
}

export function IdProofingForm({ idOptions, contactLink, getDiToken }: IdProofingFormProps) {
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
  const { setPageData, setUserData, trackEvent } = useDataLayer()

  const selectedOption = idOptions.find((opt) => opt.value === selectedIdType)
  const showIdValueInput = selectedIdType !== null && selectedIdType !== NONE_VALUE

  const REQUIRED_FIELD_ERROR = tValidation('required')
  // TODO: Use t('validation.ssnItinDigits') once key is available in dc.csv
  const SSN_ITIN_SHAPE_ERROR = 'Enter exactly 9 digits.'
  // TODO: Use t('validation.sevenOrEightDigits') once key is available in dc.csv
  const SEVEN_OR_EIGHT_DIGITS_ERROR = 'Enter 7 or 8 digits.'
  // TODO: Use t('validation.dobInvalid') once key is available in dc.csv
  const DOB_INVALID_ERROR = 'Enter a valid date of birth.'

  // Pick the user-facing error message that matches the rule's shape. The SSN
  // and ITIN messages stay verbatim so the existing wording carries through;
  // [7, 8] rules use a shared message; other shapes fall back to a generic.
  function digitRuleErrorMessage(rule: IdOptionValidation): string {
    const [min, max] = digitBounds(rule)
    if (min === max && min === 9) return SSN_ITIN_SHAPE_ERROR
    if (min === 7 && max === 8) return SEVEN_OR_EIGHT_DIGITS_ERROR
    if (min === max) return `Enter exactly ${min} digits.`
    return `Enter ${min} or ${max} digits.`
  }

  function validateFields(): boolean {
    const newDobErrors: { month?: string; day?: string; year?: string } = {}

    if (!dobMonth) newDobErrors.month = REQUIRED_FIELD_ERROR
    if (!dobDay) newDobErrors.day = REQUIRED_FIELD_ERROR
    if (!dobYear) newDobErrors.year = REQUIRED_FIELD_ERROR

    let idTypeErr: string | null = null
    if (selectedIdType === null) {
      idTypeErr = REQUIRED_FIELD_ERROR
    }

    let idError: string | null = null
    if (showIdValueInput && !idValue.trim()) {
      idError = REQUIRED_FIELD_ERROR
    }

    // Run the shared schema only when the required-field checks above haven't
    // already flagged the payload. The schema enforces SSN/ITIN digit count
    // and DOB calendar/range rules; required-ness stays field-local so each
    // field gets its own "This is required" message.
    const allRequiredFilled =
      Object.keys(newDobErrors).length === 0 && idTypeErr === null && idError === null

    if (allRequiredFilled) {
      const parsed = SubmitIdProofingRequestSchema.safeParse({
        dateOfBirth: { month: dobMonth, day: dobDay, year: dobYear },
        idType: selectedIdType === NONE_VALUE || selectedIdType === null ? null : selectedIdType,
        idValue: showIdValueInput ? idValue : null
      })

      if (!parsed.success) {
        for (const issue of parsed.error.issues) {
          const path = issue.path.join('.')
          if (path === 'dateOfBirth') {
            // Surface the same message under the year field so the user has
            // something to read. Month/day/year each render their own error
            // slot; attaching to year avoids duplicate alerts for one issue.
            newDobErrors.year = DOB_INVALID_ERROR
          } else if (path === 'idValue' && showIdValueInput) {
            idError = SSN_ITIN_SHAPE_ERROR
          }
        }
      }

      // Per-option digit-shape enforcement. The shared Zod schema only covers
      // SSN/ITIN (federal, state-agnostic); other ID types carry their own
      // rule on the IdOption. Run this after schema parsing so schema-level
      // errors win when both apply.
      if (idError === null && showIdValueInput && selectedOption?.validation) {
        if (!matchesDigitRule(idValue, selectedOption.validation)) {
          idError = digitRuleErrorMessage(selectedOption.validation)
        }
      }
    }

    setDobErrors(newDobErrors)
    setIdTypeError(idTypeErr)
    setIdValueError(idError)

    return Object.keys(newDobErrors).length === 0 && idTypeErr === null && idError === null
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitError(null)

    if (!validateFields()) return

    trackEvent(AnalyticsEvents.IDV_PRIMARY_START)

    try {
      // Best-effort: retrieve DI token if the SDK is ready
      const diSessionToken = getDiToken ? await getDiToken() : null

      const response = await submitIdProofing.mutateAsync({
        dateOfBirth: { month: dobMonth, day: dobDay, year: dobYear },
        // Map the UI "none" sentinel to null for the API
        idType: selectedIdType === NONE_VALUE || selectedIdType === null ? null : selectedIdType,
        idValue: showIdValueInput ? idValue.trim() : null,
        diSessionToken
      })

      if (response.result === 'documentVerificationRequired') {
        setPageData('idv_primary_status', 'docv_required')
        setUserData('docv_required', true, ['default', 'analytics'])
        trackEvent(AnalyticsEvents.IDV_PRIMARY_RESULT)
        if (!response.challengeId) {
          setSubmitError(
            t('idProofingStartError', 'Unable to start document verification. Please try again.')
          )
          return
        }
        clearChallengeContext()
        sessionStorage.setItem(SK_CHALLENGE_ID, response.challengeId)
        router.push(`/login/id-proofing/doc-verify?challengeId=${response.challengeId}`)
      } else if (response.result === 'failed') {
        setPageData('idv_primary_status', 'fail')
        trackEvent(AnalyticsEvents.IDV_PRIMARY_RESULT)
        // Hand off offboarding context via URL query params so the server-rendered
        // route page can branch copy (noIdProvided gets a distinct heading).
        const params = new URLSearchParams()
        if (response.canApply === false) {
          params.set('canApply', 'false')
        }
        if (response.offboardingReason) {
          params.set('reason', response.offboardingReason)
        }
        const query = params.toString()
        router.push(`/login/id-proofing/off-boarding${query ? `?${query}` : ''}`)
      } else {
        setPageData('idv_primary_status', 'success')
        trackEvent(AnalyticsEvents.IDV_PRIMARY_RESULT)
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
            onChange={(e) => {
              // When the option carries a digit-count rule, strip non-digits
              // as the user types. maxLength on the input caps length at the
              // rule's upper bound, so pasted input like "555-44-3333" lands
              // in state as "555443333" (and is clipped to maxLength).
              const raw = e.target.value
              const next = selectedOption?.validation ? raw.replace(/\D/g, '') : raw
              setIdValue(next)
            }}
            autoComplete="off"
            isRequired
            {...(selectedOption?.validation
              ? {
                  inputMode: 'numeric' as const,
                  maxLength: digitBounds(selectedOption.validation)[1]
                }
              : {})}
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
