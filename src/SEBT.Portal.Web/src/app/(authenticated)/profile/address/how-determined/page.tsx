'use client'

import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { Button, getState } from '@sebt/design-system'

export default function HowAddressDeterminedPage() {
  const { t } = useTranslation('dashboard')
  const { t: tCommon } = useTranslation('common')
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
    <div className="grid-container maxw-tablet padding-top-4">
      <h1 className="font-heading-xl text-bold margin-bottom-3">{t('title')}</h1>

      <p className="margin-top-5 margin-bottom-3">{t('body1')}</p>

      <p className="margin-bottom-3">{t('body2')}</p>

      <p className="margin-bottom-3">
        <Link
          href="/cards/info"
          className="usa-link text-bold"
        >
          {t('action2')}
        </Link>
      </p>

      <p className="margin-bottom-3">{t('body3')}</p>

      <p className="margin-bottom-3">
        <Link
          href="/contact"
          className="usa-link text-bold"
        >
          {t('action3')}
        </Link>
      </p>

      <div className="margin-top-4">
        <Button
          variant="outline"
          type="button"
          onClick={() => router.back()}
        >
          {tCommon('back')}
        </Button>
      </div>
    </div>
  )
}
