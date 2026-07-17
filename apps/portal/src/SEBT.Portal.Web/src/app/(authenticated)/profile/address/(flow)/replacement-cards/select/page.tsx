'use client'

import { useTranslation } from 'react-i18next'

import { CardSelection } from '@/features/address/components/CardSelection'
import { useHouseholdData } from '@/features/household'
import { useFlowStartAnalytics } from '@/hooks/useFlowStartAnalytics'
import { AnalyticsEvents } from '@sebt/analytics'

export default function CardSelectionPage() {
  const { t } = useTranslation('optionalId')
  const { data, isLoading } = useHouseholdData()
  const isReady = !isLoading && !!data

  useFlowStartAnalytics(AnalyticsEvents.CARD_REPLACEMENT_START, isReady)

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">{t('title')}</h1>
      <CardSelection />
    </div>
  )
}
