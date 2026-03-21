'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { CoLoadedInfo } from '@/features/address/components/CoLoadedInfo'
import { getState } from '@/lib/state'

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
      <h1>{t('coLoadedInfoTitle', 'A few things to know before replacing cards')}</h1>
      <CoLoadedInfo />
    </div>
  )
}
