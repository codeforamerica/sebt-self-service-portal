'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import type { Address, SummerEbtCase } from '@/features/household/api/schema'
import { Button, getState } from '@sebt/design-system'

interface ConfirmAddressProps {
  summerEbtCase: SummerEbtCase
  address: Address
  /** URL to navigate to when user confirms the address. */
  confirmPath: string
  /** URL to navigate to when user wants to change the address. */
  changePath: string
}

export function ConfirmAddress({
  summerEbtCase,
  address,
  confirmPath,
  changePath
}: ConfirmAddressProps) {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()

  const [selection, setSelection] = useState<'yes' | 'no' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const errorRef = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    if (error) {
      errorRef.current?.focus()
    }
  }, [error])

  const currentState = getState()
  const childName =
    summerEbtCase.childFirstName && summerEbtCase.childLastName
      ? `${summerEbtCase.childFirstName} ${summerEbtCase.childLastName}`
      : ''
  const subtitle =
    currentState === 'co' && summerEbtCase.ebtCardLastFour
      ? `Replace card ending in ${summerEbtCase.ebtCardLastFour}`
      : childName
        ? `Replace ${childName}'s card`
        : null

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    if (selection === null) {
      setError(t('selectOneError', 'Please select an option.'))
      return
    }

    if (selection === 'yes') {
      router.push(confirmPath)
    } else {
      router.push(changePath)
    }
  }

  return (
    <form
      className="usa-form maxw-full"
      onSubmit={handleSubmit}
      noValidate
    >
      {subtitle && (
        <p className="text-base-dark margin-bottom-1">
          {/* TODO: Use t('replaceCardFor') once key is available in CSV */}
          {subtitle}
        </p>
      )}

      <div className="margin-bottom-3">
        {address.streetAddress1 && (
          <p className="text-bold margin-bottom-05">{address.streetAddress1}</p>
        )}
        {address.streetAddress2 && (
          <p className="text-bold margin-bottom-05">{address.streetAddress2}</p>
        )}
        <p className="text-bold">
          {address.city}, {address.state} {address.postalCode}
        </p>
      </div>

      <p className="usa-hint margin-bottom-3">
        {t('requiredFieldsNote', 'Asterisks (*) indicate a required field')}
      </p>

      <fieldset
        className="usa-fieldset"
        aria-label={t('selectOneLabel', 'Select one')}
        aria-describedby={error ? 'confirm-address-error' : undefined}
      >
        <legend className="usa-legend text-bold">
          {t('selectOneLabel', 'Select one')}
          <span className="text-secondary-dark"> *</span>
        </legend>

        {error && (
          <span
            ref={errorRef}
            id="confirm-address-error"
            className="usa-error-message"
            role="alert"
            tabIndex={-1}
          >
            {error}
          </span>
        )}

        <div className="usa-radio">
          <input
            className="usa-radio__input usa-radio__input--tile"
            type="radio"
            id="confirm-address-yes"
            name="confirmAddress"
            value="yes"
            checked={selection === 'yes'}
            onChange={() => {
              setSelection('yes')
              setError(null)
            }}
          />
          <label
            className="usa-radio__label text-bold"
            htmlFor="confirm-address-yes"
          >
            {tCommon('yes', 'Yes')}
          </label>
        </div>

        <div className="usa-radio">
          <input
            className="usa-radio__input usa-radio__input--tile"
            type="radio"
            id="confirm-address-no"
            name="confirmAddress"
            value="no"
            checked={selection === 'no'}
            onChange={() => {
              setSelection('no')
              setError(null)
            }}
          />
          <label
            className="usa-radio__label text-bold"
            htmlFor="confirm-address-no"
          >
            {tCommon('no', 'No')}
          </label>
        </div>
      </fieldset>

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={() => router.back()}
        >
          {tCommon('back', 'Back')}
        </Button>
        <Button type="submit">{tCommon('continue', 'Continue')}</Button>
      </div>
    </form>
  )
}
