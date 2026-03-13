'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

export function LandingPage() {
  const { t } = useTranslation('landing')
  const router = useRouter()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1>{t('title')}</h1>
        <p className="usa-intro">{t('body')}</p>
        <Button onClick={() => router.push('/disclaimer')}>
          {t('cta')}
        </Button>
      </div>
    </div>
  )
}
