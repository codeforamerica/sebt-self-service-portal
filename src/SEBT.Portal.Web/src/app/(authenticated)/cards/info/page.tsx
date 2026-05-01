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

const FIS_PHONE_HREF = 'tel:+18883049167'

export default function CardInfoPage() {
  const { t: tInfo } = useTranslation('confirmInfo')
  const { t: tResult } = useTranslation('result')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { data, isLoading, isError } = useHouseholdData()
  const { session } = useAuth()
  const { setPageData } = useDataLayer()
  const isDC = getState() === 'dc'

  useEffect(() => {
    if (!isDC) {
      router.replace('/dashboard')
    }
  }, [isDC, router])

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

  if (!isDC || isLoading) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading…</span>
      </div>
    )
  }

  if (isError || !data) {
    return (
      <div className="grid-container maxw-tablet padding-top-4">
        <Alert variant="error">Unable to load card details. Please try again.</Alert>
      </div>
    )
  }

  // The "Go to the dashboard" alert only makes sense when there's at least one
  // SUN Bucks card on the dashboard for the user to act on. Fully co-loaded
  // households have no replace-card button waiting for them, so suppress it.
  const hasSunBucksCard = data.summerEbtCases.some((c) => c.issuanceType === 'SummerEbt')

  const replaceCardParagraphs = tResult('replaceCardBody1')
    .split(/\r?\n\r?\n/)
    .filter(Boolean)
  const officeAddresses = tResult('replaceCardBody3').split(/\r?\n/).filter(Boolean)

  return (
    <div className="grid-container maxw-tablet padding-top-4">
      <h1 className="font-sans-xl text-ink margin-bottom-4">
        {tInfo('coLoadedInfoTitle', 'Getting a replacement SNAP or TANF EBT card')}
      </h1>

      {replaceCardParagraphs.map((paragraph) => (
        <p
          key={paragraph}
          className="margin-bottom-3"
        >
          {paragraph}
        </p>
      ))}

      <p className="margin-bottom-2">{tResult('replaceCardBody2')}</p>
      <ul className="usa-list usa-list--small-bullets">
        {officeAddresses.map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ul>

      <p className="margin-top-2 margin-bottom-1">
        {/* TODO: Replace with t('replaceCardByMail') once the key is added to the CSV. */}
        You can also request a replacement EBT card by mail by calling FIS at{' '}
        <a
          href={FIS_PHONE_HREF}
          className="usa-link"
          data-analytics-cta="fis_phone_call"
          data-analytics-cta-destination-type="external_only"
        >
          (888) 304-9167
        </a>
        .
      </p>

      {hasSunBucksCard && (
        <Alert
          variant="info"
          slim
          className="margin-y-3"
        >
          {tResult('replaceCardBody4')}{' '}
          <Link
            href="/dashboard"
            className="usa-link"
          >
            {/* TODO: Use a dedicated key once "S6 - Request Replacement Card FIS - Action"
                stops colliding with S5's "action" key in the result namespace. */}
            Go to the dashboard
          </Link>
        </Alert>
      )}

      <Button
        variant="outline"
        type="button"
        onClick={() => router.back()}
      >
        {tCommon('back', 'Back')}
      </Button>
    </div>
  )
}
