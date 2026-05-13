'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'

import { Alert, Button, getState } from '@sebt/design-system'

import type { UpdateAddressRequest } from '../../api/schema'

interface ReplacementCardPromptProps {
  address: UpdateAddressRequest
}

export function ReplacementCardPrompt({ address }: ReplacementCardPromptProps) {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const { t: tValidation } = useTranslation('validation')

  const router = useRouter()
  const currentState = getState()

  const [selection, setSelection] = useState<'yes' | 'no' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const errorRef = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    if (error) {
      errorRef.current?.focus()
    }
  }, [error])

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    if (selection === null) {
      setError(tValidation('selectOption'))
      return
    }

    if (selection === 'no') {
      router.push('/dashboard?addressUpdated=true')
    } else {
      router.push('/profile/address/replacement-cards/select')
    }
  }

  return (
    <form
      className="usa-form maxw-full"
      onSubmit={handleSubmit}
      noValidate
    >
      <div className="margin-bottom-3">
        <p className="text-bold margin-bottom-05">{address.streetAddress1}</p>
        {address.streetAddress2 && (
          <p className="text-bold margin-bottom-05">{address.streetAddress2}</p>
        )}
        <p className="text-bold">
          {address.city}, {address.state} {address.postalCode}
        </p>
      </div>

      <p>{t('replacementCardsBody1')}</p>
      <ul className="usa-list">
        <li>{t('replacementCardsBody2')}</li>
      </ul>

      {currentState === 'dc' && (
        <Alert
          variant="info"
          slim
          className="margin-bottom-3"
        >
          {/* TODO update copy */}
          {t(
            'snapTanfCallout',
            'If your child is eligible for DC SUN Bucks through SNAP or TANF participation, they will not receive a DC SUN Bucks card.'
          )}
        </Alert>
      )}

      <p className="usa-hint margin-bottom-3">{tCommon('requiredFields')}</p>

      <fieldset
        className="usa-fieldset"
        aria-label={tCommon('selectOne')}
        aria-describedby={error ? 'replacement-choice-error' : undefined}
      >
        <legend className="usa-legend text-bold">
          {tCommon('selectOne')}
          <span className="text-secondary-dark"> *</span>
        </legend>

        {error && (
          <span
            ref={errorRef}
            id="replacement-choice-error"
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
            id="replacement-yes"
            name="replacementChoice"
            value="yes"
            checked={selection === 'yes'}
            onChange={() => {
              setSelection('yes')
              setError(null)
            }}
          />
          <label
            className="usa-radio__label text-bold"
            htmlFor="replacement-yes"
          >
            {tCommon('yes')}
          </label>
        </div>

        <div className="usa-radio">
          <input
            className="usa-radio__input usa-radio__input--tile"
            type="radio"
            id="replacement-no"
            name="replacementChoice"
            value="no"
            checked={selection === 'no'}
            onChange={() => {
              setSelection('no')
              setError(null)
            }}
          />
          <label
            className="usa-radio__label text-bold"
            htmlFor="replacement-no"
          >
            {tCommon('no')}
          </label>
        </div>
      </fieldset>

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={() => router.back()}
        >
          {tCommon('back')}
        </Button>
        <Button type="submit">{tCommon('continue')}</Button>
      </div>
    </form>
  )
}
