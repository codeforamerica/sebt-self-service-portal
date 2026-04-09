'use client'

import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { flushSync } from 'react-dom'
import { useTranslation } from 'react-i18next'

import { Button, getState } from '@sebt/design-system'

import type { UpdateAddressRequest } from '../../api/schema'
import { useAddressFlow } from '../../context'

const DEFAULT_REDIRECT = '/profile/address/replacement-cards'

export function SuggestedAddress() {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const currentState = getState()
  const { validationResult, enteredAddress, setAddress, clearValidationResult } = useAddressFlow()

  const isAbbreviated = validationResult?.reason === 'abbreviated' && currentState === 'dc'

  // Build the suggested address as UpdateAddressRequest shape
  const suggestedAddr: UpdateAddressRequest | null = validationResult?.suggestedAddress
    ? {
        streetAddress1: validationResult.suggestedAddress.streetAddress1 ?? '',
        streetAddress2: validationResult.suggestedAddress.streetAddress2 ?? undefined,
        city: validationResult.suggestedAddress.city ?? '',
        state: validationResult.suggestedAddress.state ?? '',
        postalCode: validationResult.suggestedAddress.postalCode ?? ''
      }
    : null

  const [selection, setSelection] = useState<'suggested' | 'entered'>('suggested')

  function handleContinue() {
    const selectedAddress = selection === 'suggested' ? suggestedAddr : enteredAddress
    if (selectedAddress) {
      flushSync(() => setAddress(selectedAddress))
      router.push(DEFAULT_REDIRECT)
    }
  }

  function handleBack() {
    clearValidationResult()
    router.push('/profile/address')
  }

  // Use abbreviated copy for DC 30-char abbreviation, suggested copy otherwise
  const title = isAbbreviated
    ? t('abbreviatedTitle', 'We abbreviated your address')
    : t('suggestedTitle', 'Check the address')
  const body = isAbbreviated
    ? t('abbreviatedBody1', 'We updated the street address to a format we can accept.')
    : t(
        'suggestedBody',
        'We updated the address you entered. If correct, use the suggested address.'
      )

  // For abbreviated, show additional context about the 30-char limit
  const bodyDetail = isAbbreviated
    ? t('abbreviatedBody4', 'If the suggested address is correct, tap "Use suggested address".')
    : null

  // Labels for the radio group items
  const suggestedLabel = isAbbreviated
    ? t('abbreviatedBody2', 'Suggested address')
    : tCommon('suggestedAddress', 'Suggested address')
  const enteredLabel = isAbbreviated
    ? t('abbreviatedBody3', 'Address you entered')
    : tCommon('addressYouEntered', 'Address you entered')

  function formatAddress(addr: UpdateAddressRequest | null) {
    if (!addr) return null
    return (
      <div className="margin-top-05">
        <p className="margin-y-0">
          {addr.streetAddress1}
          {addr.streetAddress2 && (
            <>
              <br />
              {addr.streetAddress2}
            </>
          )}
          <br />
          {addr.city}, {addr.state} {addr.postalCode}
        </p>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">{title}</h1>
      <p>{body}</p>
      {bodyDetail && <p>{bodyDetail}</p>}

      <p className="font-sans-3xs text-base margin-bottom-0">
        {t('requiredFieldNote', 'Asterisks (*) indicate a required field.')}
      </p>

      <fieldset className="usa-fieldset margin-top-3">
        <legend className="usa-legend">
          {t('suggestedLabelSelect', 'Select the address to use')}
          <span className="text-secondary-dark"> *</span>
        </legend>

        <div className="usa-radio margin-top-2">
          <input
            className="usa-radio__input"
            id="address-suggested"
            type="radio"
            name="address-selection"
            value="suggested"
            checked={selection === 'suggested'}
            onChange={() => setSelection('suggested')}
          />
          <label
            className="usa-radio__label"
            htmlFor="address-suggested"
          >
            <span className="text-bold">{suggestedLabel}</span>
            {formatAddress(suggestedAddr)}
          </label>
        </div>

        <div className="usa-radio margin-top-2">
          <input
            className="usa-radio__input"
            id="address-entered"
            type="radio"
            name="address-selection"
            value="entered"
            checked={selection === 'entered'}
            onChange={() => setSelection('entered')}
          />
          <label
            className="usa-radio__label"
            htmlFor="address-entered"
          >
            <span className="text-bold">{enteredLabel}</span>
            {formatAddress(enteredAddress)}
          </label>
        </div>
      </fieldset>

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={handleBack}
        >
          {tCommon('back', 'Back')}
        </Button>
        <Button
          type="button"
          onClick={handleContinue}
        >
          {tCommon('continue', 'Continue')}
        </Button>
      </div>
    </div>
  )
}
