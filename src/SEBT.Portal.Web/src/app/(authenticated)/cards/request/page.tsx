'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { CardSelection } from '@/features/address/components/CardSelection'
import { useHouseholdData } from '@/features/household'

export default function RequestReplacementCardsPage() {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { data, isLoading } = useHouseholdData()
  const canRequestReplacementCard = data?.allowedActions?.canRequestReplacementCard ?? true

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
        <span className="usa-sr-only">{tCommon('loading', 'Loading...')}</span>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('cardSelectionPageTitle', 'Which card would you like to replace?')}
      </h1>
      <CardSelection confirmPath="/cards/request/confirm" />
    </div>
  )
}
