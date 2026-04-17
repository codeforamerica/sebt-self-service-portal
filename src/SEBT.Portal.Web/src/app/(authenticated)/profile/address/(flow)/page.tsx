'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useMemo } from 'react'
import { useTranslation } from 'react-i18next'

import { AddressForm } from '@/features/address/components/AddressForm'
import { useHouseholdData } from '@/features/household'

// TODO (DC-153): Card-flow entry point — when accessed via /profile/address?from=cards,
// the form should return the user to the card replacement flow on completion
// instead of the replacement card prompt. See DC-02/CO-01 mockups.

export default function AddressFormPage() {
  const { t } = useTranslation('confirmInfo')
  const { data, isLoading } = useHouseholdData()
  const router = useRouter()

  const canChangeAddress = useMemo(
    () => data?.summerEbtCases.some((c) => c.allowAddressChange) ?? false,
    [data?.summerEbtCases]
  )

  useEffect(() => {
    if (!isLoading && !canChangeAddress) {
      router.replace('/profile')
    }
  }, [isLoading, canChangeAddress, router])

  if (isLoading || !canChangeAddress) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading…</span>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('pageTitle', 'Tell us where to safely send your mail')}
      </h1>
      <p className="usa-hint">
        {t('requiredFieldsNote', 'Asterisks (*) indicate a required field')}
      </p>
      <AddressForm initialAddress={data?.addressOnFile ?? null} />
    </div>
  )
}
