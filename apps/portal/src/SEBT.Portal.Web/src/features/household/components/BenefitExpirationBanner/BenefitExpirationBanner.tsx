'use client'

import { useTranslation } from 'react-i18next'

import { useFeatureFlag } from '@/features/feature-flags'
import { Alert } from '@sebt/design-system'

// Keys map to CSV: "S2 - Portal Dashboard - Alert {Title,Body} Expungement"
/**
 * Persistent dashboard banner shown while applications are closed (DC-701):
 * benefits for the season were issued and expire 122 days after issuance.
 * Driven by the enable_apply flag; a missing flag or failed fetch reads as
 * closed, which is the safe direction once the application window has ended.
 */
export function BenefitExpirationBanner() {
  const { t } = useTranslation('dashboard')
  const applyOpen = useFeatureFlag('enable_apply')

  if (applyOpen) {
    return null
  }

  return (
    <Alert
      variant="warning"
      heading={t('alertTitleExpungement')}
      headingClassName="font-sans-md text-semibold line-height-sans-4"
      textClassName="font-sans-md line-height-sans-4"
    >
      {t('alertBodyExpungement')}
    </Alert>
  )
}
