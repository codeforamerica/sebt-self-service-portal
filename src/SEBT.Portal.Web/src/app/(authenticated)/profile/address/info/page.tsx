'use client'

import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { useHouseholdData } from '@/features/household'
import { Button, getState } from '@sebt/design-system'

export default function CoLoadedAddressInfoPage() {
  const { t: tDashboard } = useTranslation('dashboard')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { data, isLoading } = useHouseholdData()
  const isDC = getState() === 'dc'

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
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-ink">{tDashboard('coLoadedAddressUpdateTitle')}</h1>

      <p>{tDashboard('coLoadedAddressUpdateBody1')}</p>

      <p>{tDashboard('coLoadedAddressUpdateBody2')}</p>
      <p>
        <Link
          href="/cards/info"
          className="usa-link"
        >
          {tDashboard('coLoadedAddressUpdateAction2')}
        </Link>
      </p>

      <p>{tDashboard('coLoadedAddressUpdateBody3')}</p>
      <p>
        <Link
          href="/contact"
          className="usa-link"
        >
          {tDashboard('coLoadedAddressUpdateAction3')}
        </Link>
      </p>

      <Button
        variant="outline"
        type="button"
        onClick={() => router.back()}
        className="margin-top-3"
      >
        {tCommon('back', 'Back')}
      </Button>
    </div>
  )
}
