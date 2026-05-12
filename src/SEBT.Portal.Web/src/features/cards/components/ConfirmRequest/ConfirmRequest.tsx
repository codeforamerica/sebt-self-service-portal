'use client'

import { useRouter } from 'next/navigation'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'

import type { Address, SummerEbtCase } from '@/features/household/api/schema'
import { Alert, Button, getState, RichText } from '@sebt/design-system'

import { useRequestCardReplacement } from '../../api/client'

interface ConfirmRequestProps {
  cases: SummerEbtCase[]
  address: Address
  onBack: () => void
}

export function ConfirmRequest({ cases, address, onBack }: ConfirmRequestProps) {
  const { t } = useTranslation('result')
  const { t: tDev } = useTranslation('dev')
  const { t: tCommon } = useTranslation('common')
  const { t: tDashboard } = useTranslation('dashboard')

  const router = useRouter()
  const currentState = getState()
  const mutation = useRequestCardReplacement()
  const [error, setError] = useState<string | null>(null)

  const caseRefs = cases
    .filter((c): c is SummerEbtCase & { summerEBTCaseID: string } => c.summerEBTCaseID != null)
    .map((c) => ({
      summerEbtCaseId: c.summerEBTCaseID,
      applicationId: c.applicationId ?? null,
      applicationStudentId: c.applicationStudentId ?? null
    }))

  function handleSubmit() {
    setError(null)
    mutation.mutate(
      { caseRefs },
      {
        onSuccess: () => {
          router.push('/dashboard?flash=card_replaced')
        },
        onError: () => {
          setError(tDashboard('alertCardReplaceError'))
        }
      }
    )
  }

  // t('body') is \n-delimited list items — split and filter empties
  const replacingCards = t('body').split('\n').filter(Boolean)

  return (
    <div>
      <h1 className="font-sans-xl text-primary">{t('title')}</h1>

      <div className="margin-top-05">
        <ul className="usa-list margin-top-2">
          {replacingCards.map((item, index) => (
            <li key={index}>
              <RichText>{item}</RichText>
            </li>
          ))}
        </ul>
      </div>

      <div className="usa-card__container margin-top-3">
        <div className="usa-card__body">
          <h2 className="usa-card__heading font-sans-md">{t('summaryTitle')}</h2>

          <ul className="usa-list usa-list--unstyled">
            {cases.map((c) => (
              <li
                key={c.summerEBTCaseID}
                className="margin-bottom-1"
              >
                <span className="text-bold">
                  {/* TODO update */}
                  {c.childFirstName} {c.childLastName}&apos;s card
                </span>
                {currentState === 'co' && c.ebtCardLastFour && (
                  <span className="display-block text-base-dark">
                    {/* TODO: Use t('pre-title') once key is updated in CSV */}
                    Card number: {c.ebtCardLastFour} (last 4 digits)
                  </span>
                )}
              </li>
            ))}
          </ul>

          <p className="margin-top-2">{t('summaryAddress')}</p>

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
          {tCommon('back')}
        </Button>
        <Button
          type="button"
          onClick={handleSubmit}
          disabled={mutation.isPending}
        >
          {mutation.isPending ? tDev('loading') : t('action')}
        </Button>
      </div>
    </div>
  )
}
