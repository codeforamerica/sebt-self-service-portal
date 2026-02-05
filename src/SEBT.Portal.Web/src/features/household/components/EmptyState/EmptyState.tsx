'use client'

import { Alert } from '@/components/ui'
import { useTranslation } from 'react-i18next'

// Keys map to CSV: "S2 - Portal Dashboard - Alert Applications - {Key}"
export function EmptyState() {
  const { t } = useTranslation('dashboard')

  return (
    <Alert
      variant="warning"
      heading={t('alertApplicationsTitle')}
    >
      <span>{t('alertApplicationsBody')}</span>{' '}
      <a
        href="/apply"
        className="usa-link text-bold"
      >
        {t('alertApplicationsAction')}
      </a>
    </Alert>
  )
}
