'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { CoLoadedInfo } from '@/features/address/components/CoLoadedInfo'
import { useHouseholdData } from '@/features/household'
import { getState } from '@sebt/design-system'

export default function CoLoadedInfoPage() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const { data, isLoading } = useHouseholdData()
  const isDC = getState() === 'dc'

  // This page's content is SNAP/TANF co-loaded guidance (FIS callout, keep-your-card
  // message). Users denied address update for other reasons (e.g. Pending/Denied
  // application status via AllowedCaseStatuses) get bounced to /profile, where
  // ActionButtons renders the generic "self-service unavailable" alert.
  const isCoLoaded =
    data?.benefitIssuanceType === 'SnapEbtCard' || data?.benefitIssuanceType === 'TanfEbtCard'

  useEffect(() => {
    if (!isDC) {
      router.replace('/dashboard')
      return
    }
    if (data && !isCoLoaded) {
      router.replace('/profile')
    }
  }, [isDC, data, isCoLoaded, router])

  if (!isDC || isLoading || !data || !isCoLoaded) {
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
    <div className="grid-container maxw-tablet">
      {/* TODO: Remove fallback once coLoadedAddressInfoTitle is added to CSV */}
      <h1>{t('coLoadedAddressInfoTitle', 'How to update your mailing address')}</h1>
      <CoLoadedInfo
        variant="address"
        terminal
      />
    </div>
  )
}
