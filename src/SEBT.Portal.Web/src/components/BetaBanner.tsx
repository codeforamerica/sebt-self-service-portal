'use client'

import { useFeatureFlag } from '@/features/feature-flags'
import { Alert } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

export function BetaBanner() {
  const enabled = useFeatureFlag('enable_beta_banner')
  const { t } = useTranslation('common')

  if (!enabled) {
    return null
  }

  return (
    <Alert
      variant="warning"
      className="margin-top-0"
    >
      {t('alertBeta', {
        defaultValue: 'This site is currently in beta. Some features may be incomplete or missing.'
      })}
    </Alert>
  )
}
