'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import Link from 'next/link'

import { CoLoadedInfo } from '@/features/address/components/CoLoadedInfo'
import { Alert, getState } from '@sebt/design-system'

export default function CardInfoPage() {
  const { t } = useTranslation('confirmInfo')
  const router = useRouter()
  const isDC = getState() === 'dc'

  useEffect(() => {
    if (!isDC) {
      router.replace('/dashboard')
    }
  }, [isDC, router])

  if (!isDC) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading...</span>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('coLoadedInfoTitle', 'Getting a replacement SNAP or TANF EBT card')}
      </h1>

      <Alert
        variant="info"
        slim
        className="margin-bottom-3"
      >
        {/* TODO: Use t('coLoadedSunBucksNote') once key is available in CSV */}
        You can get a new DC SUN Bucks card if you need one. Go to the portal dashboard and tap
        &quot;Request a replacement card&quot; under the child&apos;s name that has benefits issued
        to a DC SUN Bucks card.{' '}
        <Link
          href="/dashboard"
          className="usa-link"
        >
          Go to the dashboard
        </Link>
      </Alert>

      <CoLoadedInfo />
    </div>
  )
}
