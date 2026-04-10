'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { useAddressFlow } from '@/features/address'
import { ConfirmRequest } from '@/features/cards/components/ConfirmRequest'
import { useHouseholdData } from '@/features/household'
import { Alert } from '@sebt/design-system'

export default function ConfirmCardReplacementPage() {
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const searchParams = useSearchParams()
  const { address } = useAddressFlow()
  const { data, isLoading, isError } = useHouseholdData()

  const casesParam = searchParams.get('cases')
  const selectedCaseIds = casesParam ? casesParam.split(',') : []

  if (isLoading) {
    return <p>{tCommon('loading', 'Loading...')}</p>
  }

  if (isError || !data || !address) {
    return <Alert variant="error">Unable to load card replacement details. Please try again.</Alert>
  }

  const selectedCases = data.summerEbtCases.filter(
    (c) => c.summerEBTCaseID && selectedCaseIds.includes(c.summerEBTCaseID)
  )

  if (selectedCases.length === 0) {
    return (
      <Alert variant="error">
        No matching cards found. Please go back and select cards to replace.
      </Alert>
    )
  }

  const addressForConfirm = {
    streetAddress1: address.streetAddress1,
    streetAddress2: address.streetAddress2,
    city: address.city,
    state: address.state,
    postalCode: address.postalCode
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <ConfirmRequest
        cases={selectedCases}
        address={addressForConfirm}
        onBack={() => router.back()}
      />
    </div>
  )
}
