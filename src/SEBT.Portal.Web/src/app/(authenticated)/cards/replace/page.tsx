'use client'

import { useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { ConfirmAddress } from '@/features/cards/components/ConfirmAddress'
import { useHouseholdData } from '@/features/household'
import { Alert } from '@sebt/design-system'

export default function CardReplacePage() {
  const { t: tCommon } = useTranslation('common')
  const searchParams = useSearchParams()
  const { data, isLoading, isError } = useHouseholdData()

  const caseId = searchParams.get('case')

  if (isLoading) {
    return <p>{tCommon('loading', 'Loading...')}</p>
  }

  if (isError || !data || !caseId) {
    return <Alert variant="error">Unable to load card details. Please try again.</Alert>
  }

  const summerEbtCase = data.summerEbtCases.find((c) => c.summerEBTCaseID === caseId)
  const address = data.addressOnFile

  if (!summerEbtCase || !address) {
    return (
      <Alert variant="error">
        Card or address information not found. Please return to the dashboard.
      </Alert>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {/* TODO: Use t('confirmAddressTitle') once key is available in CSV */}
        Do you want the new card mailed to this address?
      </h1>
      <ConfirmAddress
        summerEbtCase={summerEbtCase}
        address={address}
        confirmPath={`/cards/replace/confirm?case=${encodeURIComponent(caseId)}`}
        changePath={`/cards/replace/address?case=${encodeURIComponent(caseId)}`}
      />
    </div>
  )
}
