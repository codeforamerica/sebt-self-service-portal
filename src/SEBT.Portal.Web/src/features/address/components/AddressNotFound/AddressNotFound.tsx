'use client'

import { useRouter } from 'next/navigation'
import { flushSync } from 'react-dom'
import { useTranslation } from 'react-i18next'

import { Alert, Button, getState, getStateLinks } from '@sebt/design-system'

import { useAddressFlow } from '../../context'

const DEFAULT_REDIRECT = '/profile/address/replacement-cards'

export function AddressNotFound() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const currentState = getState()
  const { enteredAddress, validationResult, setAddress, clearValidationResult } = useAddressFlow()
  const isBlocked = validationResult?.reason === 'blocked'

  function handleEditAddress() {
    clearValidationResult()
    router.push('/profile/address')
  }

  function handleUseThisAddress() {
    if (enteredAddress) {
      flushSync(() => setAddress(enteredAddress))
      router.push(DEFAULT_REDIRECT)
    }
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {isBlocked
          ? t('blockedTitle', "This address can't be used")
          : t('notFoundTitle', 'Are you sure this address is correct?')}
      </h1>
      <p>
        {isBlocked
          ? t(
              'blockedBody',
              `This address is not available for ${currentState === 'dc' ? 'DC' : 'CO'} SUN Bucks card delivery. Please enter a different mailing address.`
            )
          : t(
              'notFoundBody',
              "We couldn't find the address you entered. Please check the address."
            )}
      </p>

      {enteredAddress && (
        <Alert
          variant="warning"
          heading={t('notFoundAlertTitle', 'Address you entered')}
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
          {t('notFoundAlertAction', 'Edit the address')}
        </Button>
      </div>

      {currentState === 'co' && !isBlocked && (
        <div className="margin-top-2">
          <button
            type="button"
            className="usa-button usa-button--unstyled"
            onClick={handleUseThisAddress}
          >
            {t('notFoundContinue', 'Use this address')}
          </button>
        </div>
      )}

      {currentState === 'dc' && (
        <div className="margin-top-2">
          <a
            href={getStateLinks(currentState).help.contactUs}
            className="usa-link"
          >
            {t('notFoundActionHelp', 'Contact us')}
          </a>
        </div>
      )}
    </div>
  )
}
