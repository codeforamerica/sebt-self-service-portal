'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { Alert, Button, getState, getStateLinks } from '@sebt/design-system'

import { useAddressFlow } from '../../context'

const DEFAULT_REDIRECT = '/profile/address/replacement-cards'

export function AddressNotFound() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const currentState = getState()
  const { enteredAddress, setAddress, clearValidationResult } = useAddressFlow()

  function handleEditAddress() {
    clearValidationResult()
    router.push('/profile/address')
  }

  function handleUseThisAddress() {
    if (enteredAddress) {
      setAddress(enteredAddress)
      clearValidationResult()
      router.push(DEFAULT_REDIRECT)
    }
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('notFoundTitle', 'Are you sure this address is correct?')}
      </h1>
      <p>
        {t('notFoundBody', "We couldn't find the address you entered. Please check the address.")}
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

      {currentState === 'co' && (
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
