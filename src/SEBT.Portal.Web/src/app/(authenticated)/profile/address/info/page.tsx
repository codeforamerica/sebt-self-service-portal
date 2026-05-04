'use client'

import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { useAuth } from '@/features/auth'
import { useHouseholdData } from '@/features/household'
import { getColoadingStatus } from '@/lib/coloadingStatus'
import { useDataLayer } from '@sebt/analytics'
import { Alert, Button, getState } from '@sebt/design-system'

export default function CoLoadedAddressInfoPage() {
  const { t: tDashboard } = useTranslation('dashboard')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { data, isLoading, isError } = useHouseholdData()
  const { session } = useAuth()
  const { setPageData } = useDataLayer()
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

  // DC-215: tag this info-screen view with the household bucket so analytics can
  // segment who actually lands here (co_loaded_only vs mixed_eligibility vs non).
  // Tag error-state visits as `unknown` so they're counted, not dropped.
  useEffect(() => {
    if (isError) {
      setPageData('household_type', 'unknown')
      return
    }
    if (!data) return
    setPageData('household_type', getColoadingStatus(session?.isCoLoaded, data))
  }, [data, isError, session?.isCoLoaded, setPageData])

  if (isError) {
    return (
      <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
        <Alert variant="error">Unable to load address details. Please try again.</Alert>
      </div>
    )
  }

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
          data-analytics-cta="address_info_to_cards_info_cta"
        >
          {tDashboard('coLoadedAddressUpdateAction2')}
        </Link>
      </p>

      <p>{tDashboard('coLoadedAddressUpdateBody3')}</p>
      <p>
        <Link
          href="/contact"
          className="usa-link"
          data-analytics-cta="address_info_to_contact_cta"
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
