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

  const appNumber = searchParams.get('app')

  if (isLoading) {
    return <p>{tCommon('loading', 'Loading...')}</p>
  }

  if (isError || !data || !appNumber) {
    return <Alert variant="error">Unable to load card details. Please try again.</Alert>
  }

  const application = data.applications.find((a) => a.applicationNumber === appNumber)
  const address = data.addressOnFile

  if (!application || !address) {
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
        application={application}
        address={address}
        confirmPath={`/cards/replace/confirm?app=${encodeURIComponent(appNumber)}`}
        changePath={`/cards/replace/address?app=${encodeURIComponent(appNumber)}`}
      />
    </div>
  )
}
