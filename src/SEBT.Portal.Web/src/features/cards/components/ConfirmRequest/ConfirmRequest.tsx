'use client'

import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import type { Address, SummerEbtCase } from '@/features/household/api/schema'
import { Alert, Button, getState } from '@sebt/design-system'

import { useRequestCardReplacement } from '../../api/client'

interface ConfirmRequestProps {
  cases: SummerEbtCase[]
  address: Address
  onBack: () => void
}

function getStateProgramName(state: string): string {
  return state === 'dc' ? 'DC SUN Bucks' : 'Summer EBT'
}

export function ConfirmRequest({ cases, address, onBack }: ConfirmRequestProps) {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const currentState = getState()
  const mutation = useRequestCardReplacement()
  const [error, setError] = useState<string | null>(null)

  const programName = getStateProgramName(currentState)
  const caseIds = cases.map((c) => c.summerEBTCaseID).filter((id): id is string => id != null)

  function handleSubmit() {
    setError(null)
    mutation.mutate(
      { caseIds },
      {
        onSuccess: () => {
          router.push('/dashboard?flash=card_replaced')
        },
        onError: () => {
          setError(
            t(
              'cardReplacementError',
              'There was an issue requesting your replacement card. Please try again later.'
            )
          )
        }
      }
    )
  }

  return (
    <div>
      <h1 className="font-sans-xl text-primary">
        {/* TODO: Use t('confirmReplacementTitle') once key is available in CSV */}A few things to
        know before replacing {programName} cards
      </h1>

      <ul className="usa-list">
        <li>
          {/* TODO: Use t('confirmDeactivation') once key is available in CSV */}
          Once a replacement card is created, the previous card will be permanently deactivated
        </li>
        <li>
          {/* TODO: Use t('confirmDelivery') once key is available in CSV */}
          Cards will arrive by mail in around 7-10 business days
        </li>
        <li>
          {/* TODO: Use t('confirmBalanceRollover') once key is available in CSV */}
          Any remaining balance on the previous card will automatically be rolled over to the
          replacement card
        </li>
      </ul>

      <div className="usa-card__container margin-top-3">
        <div className="usa-card__body">
          <h2 className="usa-card__heading font-sans-md">
            {/* TODO: Use t('cardOrderSummary') once key is available in CSV */}
            Card order summary
          </h2>

          <ul className="usa-list usa-list--unstyled">
            {cases.map((c) => (
              <li
                key={c.summerEBTCaseID}
                className="margin-bottom-1"
              >
                <span className="text-bold">
                  {c.childFirstName} {c.childLastName}&apos;s card
                </span>
                {currentState === 'co' && c.ebtCardLastFour && (
                  <span className="display-block text-base-dark">
                    {/* TODO: Use t('cardNumberLabel') once key is available in CSV */}
                    Card number: {c.ebtCardLastFour} (last 4 digits)
                  </span>
                )}
              </li>
            ))}
          </ul>

          <p className="margin-top-2">
            {/* TODO: Use t('confirmMailingTo') once key is available in CSV */}A new card will be
            mailed to the following address:
          </p>

          <address className="margin-top-1 font-sans-sm">
            {address.streetAddress1 && (
              <span className="display-block">{address.streetAddress1}</span>
            )}
            {address.streetAddress2 && (
              <span className="display-block">{address.streetAddress2}</span>
            )}
            <span className="display-block">
              {address.city}, {address.state} {address.postalCode}
            </span>
          </address>
        </div>
      </div>

      {error && (
        <Alert
          variant="error"
          className="margin-top-3"
        >
          {error}
        </Alert>
      )}

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={onBack}
          disabled={mutation.isPending}
        >
          {tCommon('back', 'Back')}
        </Button>
        <Button
          type="button"
          onClick={handleSubmit}
          disabled={mutation.isPending}
        >
          {/* TODO: Use t('orderCard') once key is available in CSV */}
          {mutation.isPending ? tCommon('loading', 'Loading...') : 'Order card'}
        </Button>
      </div>
    </div>
  )
}
