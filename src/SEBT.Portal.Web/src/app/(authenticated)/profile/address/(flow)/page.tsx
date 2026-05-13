'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { AddressForm } from '@/features/address/components/AddressForm'
import { useHouseholdData } from '@/features/household'
import { getState } from '@sebt/design-system'

// TODO: Card-flow entry point — when accessed via /profile/address?from=cards,
// the form should return the user to the card replacement flow on completion
// instead of the replacement card prompt.

export default function AddressFormPage() {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const { t: tDev } = useTranslation('dev')

  const { data, isLoading } = useHouseholdData()
  const router = useRouter()
  const canUpdateAddress = data?.allowedActions?.canUpdateAddress ?? true

  const isDC = getState() === 'dc'

  useEffect(() => {
    if (!isLoading && data && !canUpdateAddress) {
      router.replace(isDC ? '/profile/address/info' : '/dashboard')
    }
  }, [isLoading, data, canUpdateAddress, isDC, router])

  if (isLoading || (data && !canUpdateAddress)) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">{tDev('loading')}</span>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">{t('titleYour')}</h1>
      <p className="usa-hint">{tCommon('requiredFields')}</p>
      <AddressForm initialAddress={data?.addressOnFile ?? null} />
    </div>
  )
}
