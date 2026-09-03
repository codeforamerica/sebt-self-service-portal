'use client'

import { Button } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'
import { AdentifiPixels } from '@sebt/analytics'
import { env } from '@/lib/env'
import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'

export function DisclaimerPage() {
  const { t } = useTranslation('disclaimer')
  const { pageTitleText } = getStateConfig(getState())
  const router = useRouter()

  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1 className={`font-family-sans ${pageTitleText}`}>{t('title')}</h1>
        <div className="usa-prose">
          <p>
            <strong>{t('body1')}</strong>{' '}
          </p>
          <p>{t('body2')}</p>
          <p>
            <strong>{t('body3')}</strong>{' '}
          </p>
          <p>{t('body4')}</p>
        </div>
        <div className="margin-top-4">
          <Button
            variant="outline"
            onClick={() => router.push('/')}
          >
            {t('back', { ns: 'common' })}
          </Button>
          <Button onClick={() => router.push('/check')}>{t('continue', { ns: 'common' })}</Button>
        </div>
      </div>

      {env.NEXT_PUBLIC_ADENTIFI_PIXEL_APPLY_NOW && (
        <AdentifiPixels pixelId={env.NEXT_PUBLIC_ADENTIFI_PIXEL_APPLY_NOW} />
      )}
    </div>
  )
}
