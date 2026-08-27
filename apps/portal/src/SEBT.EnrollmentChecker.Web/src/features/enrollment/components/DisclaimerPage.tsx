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
        <h1 className={`font-family-sans font-sans-xl margin-bottom-4 ${pageTitleText}`}>
          {t('title')}
        </h1>
        {/* Two paragraphs, each a lead sentence on its own line above the text
            it introduces — display-block breaks the line without adding a gap. */}
        <div className="usa-prose">
          <p>
            <strong className="display-block">{t('body1')}</strong>
            {t('body2')}
          </p>
          <p className="margin-top-3">
            <strong className="display-block">{t('body3')}</strong>
            {t('body4')}
          </p>
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
