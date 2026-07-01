'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import { getApplyHref } from '@/lib/applyHref'
import { Alert } from '@sebt/design-system'

// Keys map to CSV: "S2 - Portal Dashboard - Alert Applications - {Key}"
export function EmptyState() {
  const { t, i18n } = useTranslation('dashboard')

  return (
    <Alert
      variant="warning"
      heading={t('alertApplicationsTitle')}
      headingClassName="font-sans-md text-semibold line-height-sans-4"
      textClassName="font-sans-md line-height-sans-4"
    >
      <span>{t('alertApplicationsBody')}</span>{' '}
      <Link
        href={getApplyHref(i18n.language)}
        className="usa-link font-sans-md text-bold text-ink display-block margin-top-1"
      >
        {t('alertApplicationsAction')}
      </Link>
    </Alert>
  )
}
