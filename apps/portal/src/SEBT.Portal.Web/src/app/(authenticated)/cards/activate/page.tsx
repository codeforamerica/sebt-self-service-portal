'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { Button } from '@sebt/design-system'

// EBT Customer Service activation line. The number also appears inside the
// `dashboard.body` instructions copy; this constant is the dial target for the
// "Tap to call" link. Distinct from the FIS replacement line used by cards/info.
const EBT_ACTIVATION_PHONE_HREF = 'tel:+18883282656'

export default function CardActivationPage() {
  const { t } = useTranslation('dashboard')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()

  const paragraphs = t('body')
    .split(/\r?\n\r?\n/)
    .filter(Boolean)

  return (
    <div className="grid-container maxw-tablet padding-top-4">
      <h1 className="font-sans-xl text-primary margin-bottom-4">{t('title')}</h1>

      {paragraphs.map((paragraph) => (
        <p
          key={paragraph}
          className="margin-bottom-3"
        >
          {paragraph}
        </p>
      ))}

      <p className="margin-bottom-4">
        <a
          href={EBT_ACTIVATION_PHONE_HREF}
          className="usa-link text-bold"
          data-analytics-cta="card_activation_phone_call"
          data-analytics-cta-destination-type="external_only"
        >
          {t('action')}
        </a>
      </p>

      <Button
        variant="outline"
        type="button"
        onClick={() => router.back()}
      >
        {tCommon('back')}
      </Button>
    </div>
  )
}
