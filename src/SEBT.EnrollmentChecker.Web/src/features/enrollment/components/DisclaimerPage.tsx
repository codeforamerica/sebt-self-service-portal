'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

export function DisclaimerPage() {
  const { t } = useTranslation('disclaimer')
  const router = useRouter()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1>{t('title')}</h1>
        <div className="usa-prose">
          <p>{t('body')}</p>
        </div>
        <div className="margin-top-4">
          <Button variant="outline" onClick={() => router.push('/')}>
            {t('back', { ns: 'common' })}
          </Button>
          <Button onClick={() => router.push('/check')}>
            {t('continue', { ns: 'common' })}
          </Button>
        </div>
      </div>
    </div>
  )
}
