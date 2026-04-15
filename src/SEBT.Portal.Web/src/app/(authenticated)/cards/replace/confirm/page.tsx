'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { ConfirmRequest } from '@/features/cards/components/ConfirmRequest'
import { useHouseholdData } from '@/features/household'
import { Alert } from '@sebt/design-system'

export default function CardReplaceConfirmPage() {
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const searchParams = useSearchParams()
  const { data, isLoading, isError } = useHouseholdData()

  const caseId = searchParams.get('case')

  if (isLoading) {
    return <p>{tCommon('loading', 'Loading...')}</p>
  }

  if (isError || !data || !caseId) {
    return <Alert variant="error">Unable to load card replacement details. Please try again.</Alert>
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
      <ConfirmRequest
        cases={[summerEbtCase]}
        address={address}
        onBack={() => router.back()}
      />
    </div>
  )
}
