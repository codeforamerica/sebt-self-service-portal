'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'

import { Alert, Button, InputField, getState, getStateLinks } from '@sebt/design-system'

import type { Address } from '@/features/household/api'

import { isValidZip, useUpdateAddress } from '../../api'
import { useAddressFlow } from '../../context'
import { STATE_ABBREVIATIONS, US_STATE_OPTIONS } from './usStates'

interface AddressFormProps {
  initialAddress: Address | null
  /** Override the default redirect path after successful address update. */
  redirectPath?: string
}

interface FieldErrors {
  streetAddress1?: string
  city?: string
  state?: string
  postalCode?: string
}

const STATE_DEFAULTS: Record<string, { city: string; state: string }> = {
  dc: { city: 'Washington', state: 'DC' },
  co: { city: '', state: 'CO' }
}

/** Resolves backend/form state to the USPS code used as the select value (labels stay full names). */
function resolveStateValue(value: string | null | undefined, fallback: string): string {
  if (!value) return fallback
  const trimmed = value.trim()
  if (!trimmed) return fallback

  const upper2 = trimmed.length === 2 ? trimmed.toUpperCase() : null
  if (upper2 && upper2 in STATE_ABBREVIATIONS) return upper2

  for (const [code, name] of Object.entries(STATE_ABBREVIATIONS)) {
    if (name === trimmed) return code
  }
  for (const [code, name] of Object.entries(STATE_ABBREVIATIONS)) {
    if (name.toLowerCase() === trimmed.toLowerCase()) return code
  }

  return fallback
}

const DEFAULT_REDIRECT = '/profile/address/replacement-cards'

export function AddressForm({ initialAddress, redirectPath }: AddressFormProps) {
  const { t } = useTranslation('confirmInfo')
  const { t: tValidation } = useTranslation('validation')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const updateAddress = useUpdateAddress()
  const { setAddress, setValidationResult } = useAddressFlow()
  const errorSummaryRef = useRef<HTMLDivElement>(null)

  const currentState = getState()
  // eslint-disable-next-line security/detect-object-injection -- currentState is typed StateCode
  const defaults = STATE_DEFAULTS[currentState] ?? { city: '', state: '' }

  const [streetAddress1, setStreetAddress1] = useState(initialAddress?.streetAddress1 ?? '')
  const [streetAddress2, setStreetAddress2] = useState(initialAddress?.streetAddress2 ?? '')
  const [city, setCity] = useState(initialAddress?.city ?? defaults.city)
  const [stateValue, setStateValue] = useState(
    resolveStateValue(initialAddress?.state, defaults.state)
  )
  const [postalCode, setPostalCode] = useState(initialAddress?.postalCode ?? '')

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [submitError, setSubmitError] = useState<string | null>(null)

  const isSubmitting = updateAddress.isPending
  const hasErrors = Object.keys(fieldErrors).length > 0

  useEffect(() => {
    if (hasErrors) {
      errorSummaryRef.current?.focus()
    }
  }, [hasErrors])

  function validate(): FieldErrors {
    const errors: FieldErrors = {}
    const required = tValidation('required', 'This field is required.')

    if (!streetAddress1.trim()) {
      errors.streetAddress1 = required
    } else if (streetAddress1.trim().length > 30) {
      // TODO: Backend does not yet enforce this limit — add [MaxLength(30)] when confirmed
      errors.streetAddress1 = t(
        'streetAddressTooLong',
        'Enter a street address shorter than 30 characters.'
      )
    }
    if (!city.trim()) errors.city = required
    if (!stateValue.trim()) errors.state = required
    if (!postalCode.trim()) {
      errors.postalCode = required
    } else if (!isValidZip(postalCode.trim())) {
      errors.postalCode = t('postalCodeInvalid', 'Enter a valid 5- or 9-digit ZIP code.')
    }

    return errors
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setSubmitError(null)

    const errors = validate()
    setFieldErrors(errors)

    if (Object.keys(errors).length > 0) {
      return
    }

    const addressData = {
      streetAddress1: streetAddress1.trim(),
      streetAddress2: streetAddress2.trim() || undefined,
      city: city.trim(),
      state: stateValue.trim(),
      postalCode: postalCode.trim()
    }

    try {
      const result = await updateAddress.mutateAsync(addressData)

      if (result.status === 'valid') {
        setAddress(addressData)
        router.push(redirectPath ?? DEFAULT_REDIRECT)
        return
      }

      // too_long stays on the form with inline + banner errors
      if (result.reason === 'too_long') {
        setFieldErrors({
          streetAddress1: t(
            'streetAddressInlineError',
            'Enter a street address shorter than 30 characters'
          )
        })
        setSubmitError('too_long')
        return
      }

      // blocked, abbreviated, or suggestion: store in context and navigate
      setValidationResult(result, addressData)
      if (result.status === 'suggestion') {
        router.push('/profile/address/suggested-address')
      } else {
        router.push('/profile/address/address-not-found')
      }
    } catch (err) {
      void err
      setSubmitError(t('addressUpdateError', 'Something went wrong. Please try again.'))
    }
  }

  const showStreetLengthAlert = hasErrors && streetAddress1.trim().length > 30
  // Portal renders this alert above the header via #site-alerts in root layout.
  // If a second feature needs this pattern, refactor to a shared SiteAlertContext.
  const siteAlertsEl =
    typeof document !== 'undefined' ? document.getElementById('site-alerts') : null

  return (
    <>
      {showStreetLengthAlert &&
        siteAlertsEl &&
        createPortal(
          <Alert
            variant="error"
            className="margin-bottom-0"
            textClassName="text-bold"
          >
            {t(
              'streetAddressWarning',
              'There was an issue with the address provided. If you can, please, enter a street address shorter than 30 characters.'
            )}
            <br />
            <a
              href={getStateLinks(currentState).help.contactUs}
              className="usa-link"
            >
              {t('contactUsHelp', 'Contact us if you need more help.')}
            </a>
          </Alert>,
          siteAlertsEl
        )}

      <form
        className="usa-form maxw-full"
        onSubmit={handleSubmit}
        noValidate
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

        {hasErrors && (
          <div
            ref={errorSummaryRef}
            className="usa-alert usa-alert--error usa-alert--slim margin-bottom-2"
            role="alert"
            tabIndex={-1}
          >
            <div className="usa-alert__body">
              <p className="usa-alert__text">
                {t('formErrorSummary', 'Please correct the errors below.')}
              </p>
            </div>
          </div>
        )}

        <InputField
          label={t('labelStreetAddress', 'Street address')}
          {...(currentState === 'dc'
            ? { hint: t('hintStreetAddressDc', 'Include direction. NW, NE, SE, or SW.') }
            : {})}
          name="streetAddress1"
          value={streetAddress1}
          onChange={(e) => setStreetAddress1(e.target.value)}
          autoComplete="address-line1"
          isRequired
          {...(fieldErrors.streetAddress1 ? { error: fieldErrors.streetAddress1 } : {})}
        />

        <InputField
          label={t('labelStreetAddress2', 'Street address line 2')}
          hint={t(
            'hintStreetAddress2',
            'For example, an apartment number, unit number, floor, or PO Box.'
          )}
          name="streetAddress2"
          value={streetAddress2}
          onChange={(e) => setStreetAddress2(e.target.value)}
          autoComplete="address-line2"
        />

        <InputField
          label={t('labelCity', 'City')}
          name="city"
          value={city}
          onChange={(e) => setCity(e.target.value)}
          autoComplete="address-level2"
          isRequired
          {...(fieldErrors.city ? { error: fieldErrors.city } : {})}
        />

        <div
          className={fieldErrors.state ? 'usa-form-group usa-form-group--error' : 'usa-form-group'}
        >
          <label
            className="usa-label"
            htmlFor="address-state"
          >
            {t('labelState', 'State or territory')}
            <span className="text-secondary-dark"> *</span>
          </label>
          {fieldErrors.state && (
            <span
              className="usa-error-message"
              id="address-state-error"
              role="alert"
            >
              {fieldErrors.state}
            </span>
          )}
          <select
            id="address-state"
            className={`usa-select${fieldErrors.state ? ' usa-input--error' : ''}`}
            name="state"
            value={stateValue}
            onChange={(e) => setStateValue(e.target.value)}
            autoComplete="address-level1"
            aria-required="true"
            aria-invalid={!!fieldErrors.state}
            aria-describedby={fieldErrors.state ? 'address-state-error' : undefined}
          >
            <option value="">- Select -</option>
            {US_STATE_OPTIONS.map(({ code, name }) => (
              <option
                key={code}
                value={code}
              >
                {name}
              </option>
            ))}
          </select>
        </div>

        <InputField
          label={t('labelPostalCode', 'ZIP Code')}
          name="postalCode"
          value={postalCode}
          onChange={(e) => setPostalCode(e.target.value)}
          autoComplete="postal-code"
          isRequired
          {...(fieldErrors.postalCode ? { error: fieldErrors.postalCode } : {})}
        />

        <div className="margin-top-3 display-flex flex-row gap-2">
          <Button
            variant="outline"
            type="button"
            onClick={() => router.back()}
          >
            {tCommon('back', 'Back')}
          </Button>
          <Button
            type="submit"
            isLoading={isSubmitting}
            loadingText={`${tCommon('continue', 'Continue')}...`}
            disabled={isSubmitting}
          >
            {tCommon('continue', 'Continue')}
          </Button>
        </div>
      </form>
    </>
  )
}
