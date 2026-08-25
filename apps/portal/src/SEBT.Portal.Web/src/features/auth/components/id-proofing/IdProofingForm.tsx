'use client'

import { useRouter } from 'next/navigation'
import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { AnalyticsEvents, useDataLayer } from '@sebt/analytics'
import { Alert, Button, InputField, LoadingInterstitial, Spinner } from '@sebt/design-system'

import { useAuth } from '@/features/auth'
import {
  clearChallengeContext,
  SK_CHALLENGE_ID
} from '@/features/auth/components/doc-verify/sessionKeys'
import {
  SubmitIdProofingRequestSchema,
  useRefreshToken,
  useSubmitIdProofing,
  type IdType
} from '../../api'

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
  /** Render a horizontal rule above this option to visually separate it from preceding options. */
  dividerBefore?: boolean
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

/**
 * A field/submit message that defers translation to render time (DC-454): either an i18n key
 * in a given namespace, or a literal English string for messages that have no key yet.
 */
type Msg = { ns: 'validation' | 'dev'; key: string } | { literal: string }

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
  const { t: tProcessing } = useTranslation('step-upProcessing')
  const { t: tDev } = useTranslation('dev')

  const formId = useId()
  const months = getLocalizedMonths(i18n.language)

  const [dobMonth, setDobMonth] = useState('')
  const [dobDay, setDobDay] = useState('')
  const [dobYear, setDobYear] = useState('')
  const [selectedIdType, setSelectedIdType] = useState<IdOptionValue | null>(null)
  const [idValue, setIdValue] = useState('')

  // Error state holds deferred messages (Msg), not resolved strings, so the keyed ones
  // re-translate at render time when the user switches language (DC-454). Messages with no
  // i18n key yet are carried as literals (content gap) and stay English until a key exists.
  const [dobErrors, setDobErrors] = useState<{ month?: Msg; day?: Msg; year?: Msg }>({})
  // Composite errors that describe the date as a whole (impossible calendar date,
  // future, >120 years ago) belong to the fieldset, not to any single input.
  const [dobFieldsetError, setDobFieldsetError] = useState<Msg | null>(null)
  const [idTypeError, setIdTypeError] = useState<Msg | null>(null)
  const [idValueError, setIdValueError] = useState<Msg | null>(null)
  const [submitError, setSubmitError] = useState<Msg | null>(null)
  // Covers the full submit flow, not just the mutation. The Socure DI token
  // fetch runs before mutateAsync, so leaning on `submitIdProofing.isPending`
  // alone would leave the form on screen during a slow token call.
  const [isProcessing, setIsProcessing] = useState(false)

  const submitIdProofing = useSubmitIdProofing()
  const refreshToken = useRefreshToken()
  const { setPageData, setUserData, trackEvent } = useDataLayer()
  const { session } = useAuth()
  const isCoLoaded = session?.isCoLoaded === true

  const selectedOption = idOptions.find((opt) => opt.value === selectedIdType)
  const showIdValueInput = selectedIdType !== null && selectedIdType !== NONE_VALUE

  const REQUIRED_FIELD_ERROR: Msg = { ns: 'validation', key: 'required' }
  const SSN_ITIN_SHAPE_ERROR: Msg = { ns: 'validation', key: 'ssn' }
  const SEVEN_OR_EIGHT_DIGITS_ERROR: Msg = { ns: 'validation', key: 'idNumber' }
  const DOB_INVALID_ERROR: Msg = { ns: 'validation', key: 'validDate' }

  // Resolve a deferred message at render time so it follows a language switch (DC-454).
  // Call sites guard on the message being present.
  const resolveMsg = (m: Msg): string => {
    if ('literal' in m) return m.literal
    return m.ns === 'dev' ? tDev(m.key) : tValidation(m.key)
  }

  // Pick the user-facing error message that matches the rule's shape. SSN/ITIN and [7, 8]
  // rules reuse existing keys; other shapes have no key yet and stay as literals (content gap).
  function digitRuleErrorMessage(rule: IdOptionValidation): Msg {
    const [min, max] = digitBounds(rule)
    if (min === max && min === 9) return SSN_ITIN_SHAPE_ERROR
    if (min === 7 && max === 8) return SEVEN_OR_EIGHT_DIGITS_ERROR
    if (min === max) return { literal: `Enter exactly ${min} digits.` }
    return { literal: `Enter ${min} or ${max} digits.` }
  }

  function validateFields(): boolean {
    const newDobErrors: { month?: Msg; day?: Msg; year?: Msg } = {}
    let newDobFieldsetError: Msg | null = null

    if (!dobMonth) newDobErrors.month = REQUIRED_FIELD_ERROR
    if (!dobDay) newDobErrors.day = REQUIRED_FIELD_ERROR
    if (!dobYear) newDobErrors.year = REQUIRED_FIELD_ERROR

    let idTypeErr: Msg | null = null
    if (selectedIdType === null) {
      idTypeErr = REQUIRED_FIELD_ERROR
    }

    let idError: Msg | null = null
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
            // The schema emits dateOfBirth issues for failures that describe
            // the whole date (impossible calendar date, future, age cap), not
            // any single field. Surface at the fieldset level so we don't
            // mark an individual input invalid that's actually fine.
            newDobFieldsetError = DOB_INVALID_ERROR
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
    setDobFieldsetError(newDobFieldsetError)
    setIdTypeError(idTypeErr)
    setIdValueError(idError)

    return (
      Object.keys(newDobErrors).length === 0 &&
      newDobFieldsetError === null &&
      idTypeErr === null &&
      idError === null
    )
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitError(null)

    if (!validateFields()) return

    trackEvent(AnalyticsEvents.IDV_PRIMARY_START)
    setIsProcessing(true)

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
          setSubmitError({ ns: 'dev', key: 'alertVerificationRetry' })
          return
        }
        clearChallengeContext()
        sessionStorage.setItem(SK_CHALLENGE_ID, response.challengeId)
        router.push(`/login/id-proofing/doc-verify?challengeId=${response.challengeId}`)
      } else if (response.result === 'failed') {
        setPageData('idv_primary_status', 'fail')
        if (response.offboardingReason === 'noQualifyingHousehold') {
          setPageData('idv_primary_reason', 'no_qualifying_household')
        } else {
          // Co-loaded users reach "failed" only via SNAP/TANF + DOB mismatch (no Socure),
          // or when the backend classified the household as co-loaded-only.
          setPageData(
            'idv_primary_reason',
            isCoLoaded || response.offboardingReason === 'coLoadedOnly'
              ? 'not_found'
              : 'socure_fail'
          )
        }
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

        // A successful co-loaded match flips user.IsCoLoaded server-side, but the cookie
        // we hold was minted before the match. Refresh so the dashboard reads the updated
        // claim. Swallow failures — leave the user on a working flow if the refresh hiccups;
        // the dashboard will still load with the prior claim.
        try {
          await refreshToken.mutateAsync()
        } catch {
          // Intentionally silent.
        }

        router.push('/dashboard')
      }
    } catch (err) {
      // All errors get the same user-facing message. Raw ApiError.message may contain
      // backend wording not intended for end users — avoid displaying it directly.
      void err
      setSubmitError({ ns: 'validation', key: 'globalInternalError' })
    } finally {
      setIsProcessing(false)
    }
  }

  // While the Socure-backed submission is in flight (DI token fetch + mutation),
  // replace the form with a dedicated loading interstitial. The full flow can
  // take several seconds when Socure responds slowly; without this, users only
  // see the submit button text change to "Continue..." and any eventual outcome
  // (off-boarding navigation or an inline error) reads as "we just got an error
  // after waiting."
  //
  // The titled interstitial only renders when the active locale bundle has
  // step-upProcessing copy. States whose content sheet omits those rows (DC
  // marks them !N/A!) would otherwise see i18next leak the literal key names
  // "title"/"body" through the fallback chain — they fall back to a spinner-only
  // status region. Adding the copy upstream is enough to switch in the titled
  // interstitial; no code change needed.
  if (isProcessing || submitIdProofing.isPending) {
    const hasInterstitialCopy =
      i18n.exists('step-upProcessing:title') && i18n.exists('step-upProcessing:body')
    if (hasInterstitialCopy) {
      return (
        <LoadingInterstitial
          title={tProcessing('title')}
          message={tProcessing('body')}
        />
      )
    }
    return (
      <div
        className="padding-y-4 text-center"
        role="status"
        aria-busy="true"
        aria-live="polite"
      >
        <Spinner />
      </div>
    )
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
          {resolveMsg(submitError)}
        </Alert>
      )}

      {/* Date of birth */}
      <fieldset
        className={`usa-fieldset${dobFieldsetError ? ' usa-form-group--error' : ''}`}
        aria-describedby={`${formId}-dob-hint`}
      >
        <legend className="usa-legend">
          {t('labelDob')}
          <span className="text-secondary-dark"> *</span>
        </legend>

        <span
          className="usa-hint"
          id={`${formId}-dob-hint`}
        >
          {t('helperDob')}
        </span>

        {dobFieldsetError && (
          <span
            className="usa-error-message"
            role="alert"
          >
            {resolveMsg(dobFieldsetError)}
          </span>
        )}

        <div className="grid-row grid-gap">
          {/* Month */}
          <div className="mobile-lg:grid-col-7">
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
                  {resolveMsg(dobErrors.month)}
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
                <option value="">{`- ${tCommon('selectOne')} -`}</option>
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
          <div className="mobile-lg:grid-col-2">
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
              {...(dobErrors.day ? { error: resolveMsg(dobErrors.day) } : {})}
            />
          </div>

          {/* Year */}
          <div className="mobile-lg:grid-col-3">
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
              {...(dobErrors.year ? { error: resolveMsg(dobErrors.year) } : {})}
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
            {resolveMsg(idTypeError)}
          </span>
        )}

        {idOptions.map((option) => (
          <div
            key={option.value}
            className="margin-top-2"
          >
            {option.dividerBefore && (
              <hr
                aria-hidden="true"
                className="margin-y-2 border-0 border-top border-base-ink"
              />
            )}
            <div className="usa-radio">
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
                <span className="text-bold">{t(option.labelKey)}</span>
                {option.helperKey && (
                  <span className="usa-radio__label-description">{t(option.helperKey)}</span>
                )}
              </label>
            </div>
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
            {...(idValueError ? { error: resolveMsg(idValueError) } : {})}
          />
        </div>
      )}

      {/* No button-level busy state: the whole form unmounts into the
          processing interstitial above before this could ever render busy. */}
      <Button
        type="submit"
        className="margin-top-3 display-block"
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
