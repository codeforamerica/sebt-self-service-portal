'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { CardSelection } from '@/features/address/components/CardSelection'
import { useHouseholdData } from '@/features/household'
import { useFlowStartAnalytics } from '@/hooks/useFlowStartAnalytics'
import { AnalyticsEvents } from '@sebt/analytics'

export default function RequestReplacementCardsPage() {
  const { t: tOptional } = useTranslation('optionalId')
  const { t: tDev } = useTranslation('dev')
  const router = useRouter()
  const { data, isLoading } = useHouseholdData()
  const canRequestReplacementCard = data?.allowedActions?.canRequestReplacementCard ?? true
  const isReady = !isLoading && !!data && canRequestReplacementCard

  useFlowStartAnalytics(AnalyticsEvents.CARD_REPLACEMENT_START, isReady)

  useEffect(() => {
    if (!isLoading && data && !canRequestReplacementCard) {
      router.replace('/dashboard')
    }
  }, [isLoading, data, canRequestReplacementCard, router])

  if (isLoading || (data && !canRequestReplacementCard)) {
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
      <h1 className="font-sans-xl text-primary">{tOptional('title')}</h1>
      <CardSelection confirmPath="/cards/request/confirm" />
    </div>
  )
}
