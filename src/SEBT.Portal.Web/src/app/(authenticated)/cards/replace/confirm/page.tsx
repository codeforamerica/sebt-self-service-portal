'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { Alert } from '@/components/ui'
import { ConfirmRequest } from '@/features/cards/components/ConfirmRequest'
import { useHouseholdData } from '@/features/household'

export default function CardReplaceConfirmPage() {
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const searchParams = useSearchParams()
  const { data, isLoading, isError } = useHouseholdData()

  const appNumber = searchParams.get('app')

  if (isLoading) {
    return <p>{tCommon('loading', 'Loading...')}</p>
  }

  if (isError || !data || !appNumber) {
    return <Alert variant="error">Unable to load card replacement details. Please try again.</Alert>
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
      <ConfirmRequest
        applications={[application]}
        address={address}
        onBack={() => router.back()}
      />
    </div>
  )
}
