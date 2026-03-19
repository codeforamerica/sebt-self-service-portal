'use client'

import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { Alert } from '@/components/ui'

/**
 * Displays success alerts on the dashboard triggered by URL search params.
 * Captures alert state on first read, then cleans the params from the URL.
 * The alert persists because rendering is driven by captured state, not live params.
 * Extensible: add new param checks for future alert types (e.g., DC-153 card ordering).
 */
export function DashboardAlerts() {
  const { t } = useTranslation('dashboard')
  const searchParams = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()

  // Capture alert state from URL params on first read so the alert
  // survives the URL cleanup that follows.
  const [alerts] = useState(() => ({
    addressUpdated: searchParams.get('addressUpdated') === 'true',
    cardsRequested: searchParams.get('cardsRequested') === 'true'
  }))

  const hasAlerts = alerts.addressUpdated || alerts.cardsRequested

  useEffect(() => {
    if (hasAlerts) {
      router.replace(pathname, { scroll: false })
    }
  }, [hasAlerts, router, pathname])

  if (!hasAlerts) {
    return null
  }

  return (
    <div className="margin-bottom-3">
      {alerts.addressUpdated && !alerts.cardsRequested && (
        <Alert
          variant="success"
          heading={t('addressUpdatedHeading', 'Mailing address updated')}
        >
          {t(
            'addressUpdatedBody',
            'Your mailing address has been updated. Future correspondence will be sent to your new address.'
          )}
        </Alert>
      )}

      {alerts.addressUpdated && alerts.cardsRequested && (
        <Alert
          variant="success"
          heading={t('cardsRequestedHeading', 'Mailing address updated and cards requested')}
        >
          {t(
            'cardsRequestedBody',
            'Your mailing address has been updated and replacement cards have been requested. New cards should arrive in 7–10 business days.'
          )}
        </Alert>
      )}
    </div>
  )
}
