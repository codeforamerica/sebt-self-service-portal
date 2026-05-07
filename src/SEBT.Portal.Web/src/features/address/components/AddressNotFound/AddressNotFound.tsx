'use client'

import { useRouter } from 'next/navigation'
import { flushSync } from 'react-dom'
import { useTranslation } from 'react-i18next'

import { Alert, Button, getState, getStateLinks } from '@sebt/design-system'

import { useAddressFlow } from '../../context'

export function AddressNotFound() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const currentState = getState()
  const {
    enteredAddress,
    validationResult,
    setAddress,
    clearValidationResult,
    continuePath,
    formPath
  } = useAddressFlow()
  const isBlocked = validationResult?.reason === 'blocked'

  function handleEditAddress() {
    clearValidationResult()
    router.push(formPath)
  }

  function handleUseThisAddress() {
    if (enteredAddress) {
      flushSync(() => setAddress(enteredAddress))
      router.push(continuePath)
    }
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {isBlocked
          ? t('blockedTitle', "This address can't be used") // TODO REMOVE FALLBACK
          : t('notFoundTitle')}
      </h1>
      <p>
        {isBlocked
          ? t(
              'blockedBody',
              `This address is not available for ${currentState === 'dc' ? 'DC' : 'CO'} SUN Bucks card delivery. Please enter a different mailing address.`
            ) // TODO REMOVE FALLBACK
          : t('notFoundBody')}
      </p>

      {enteredAddress && (
        <Alert
          variant="warning"
          heading={t('notFoundAlertTitle')}
          className="margin-y-3"
        >
          {enteredAddress.streetAddress1}
          {enteredAddress.streetAddress2 && (
            <>
              <br />
              {enteredAddress.streetAddress2}
            </>
          )}
          <br />
          {enteredAddress.city}, {enteredAddress.state} {enteredAddress.postalCode}
        </Alert>
      )}

      <div className="margin-top-3">
        <Button
          type="button"
          onClick={handleEditAddress}
        >
          {t('notFoundAlertAction')}
        </Button>
      </div>

      {currentState === 'co' && !isBlocked && (
        <div className="margin-top-2">
          <button
            type="button"
            className="usa-button usa-button--unstyled"
            onClick={handleUseThisAddress}
          >
            {t('notFoundContinue')}
          </button>
        </div>
      )}

      {currentState === 'dc' && (
        <div className="margin-top-2">
          <a
            href={getStateLinks(currentState).help.contactUs}
            className="usa-link"
          >
            {t('notFoundActionHelp')}
          </a>
        </div>
      )}
    </div>
  )
}
