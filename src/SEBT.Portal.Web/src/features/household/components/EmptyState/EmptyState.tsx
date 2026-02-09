'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { Alert } from '@/components/ui'

// Keys map to CSV: "S2 - Portal Dashboard - Alert Applications - {Key}"
export function EmptyState() {
  const { t } = useTranslation('dashboard')

  return (
    <Alert
      variant="warning"
      heading={t('alertApplicationsTitle')}
    >
      <span>{t('alertApplicationsBody')}</span>{' '}
      <Link
        href="/apply"
        className="usa-link text-bold"
      >
        {t('alertApplicationsAction')}
      </Link>
    </Alert>
  )
}
