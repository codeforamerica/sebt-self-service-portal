'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { CoLoadedInfo } from '@/features/address/components/CoLoadedInfo'
import { getState } from '@sebt/design-system'

export default function CoLoadedInfoPage() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const isDC = getState() === 'dc'

  useEffect(() => {
    if (!isDC) {
      router.replace('/profile/address')
    }
  }, [isDC, router])

  if (!isDC) {
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
