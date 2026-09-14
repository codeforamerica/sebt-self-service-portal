'use client'

import { useRouter, useSearchParams } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { ConfirmAddress } from '@/features/cards/components/ConfirmAddress'
import { useHouseholdData } from '@/features/household'
import { useFlowStartAnalytics } from '@/hooks/useFlowStartAnalytics'
import { AnalyticsEvents } from '@sebt/analytics'
import { Alert } from '@sebt/design-system'

export default function CardReplacePage() {
  const { t: tDev } = useTranslation('dev')
  const { t: tValidation } = useTranslation('validation')
  const router = useRouter()
  const searchParams = useSearchParams()
  const { data, isLoading, isError } = useHouseholdData()

  const caseId = searchParams.get('case')
  const summerEbtCase = data?.summerEbtCases.find((c) => c.summerEBTCaseID === caseId)
  const address = data?.addressOnFile
  const isDenied = !!summerEbtCase && summerEbtCase.allowCardReplacement === false
  const isReady =
    !isLoading && !isError && !!data && !!caseId && !!summerEbtCase && !!address && !isDenied

  useFlowStartAnalytics(AnalyticsEvents.CARD_REPLACEMENT_START, isReady)

  useEffect(() => {
    if (!isLoading && data && isDenied) {
      router.replace('/dashboard')
    }
  }, [isLoading, data, isDenied, router])

  if (isLoading) {
    return <p>{tDev('loading')}</p>
  }

  if (data && isDenied) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">{tDev('loading')}</span>
      </div>
    )
  }

  if (isError || !data || !caseId) {
    return <Alert variant="error">{tValidation('globalInternalError')}</Alert>
  }

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
