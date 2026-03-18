'use client'

import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'

import { Alert } from '@/components/ui'

/**
 * Displays success alerts on the dashboard triggered by URL search params.
 * Cleans the params after rendering so a page refresh doesn't re-show alerts.
 * Extensible: add new param checks for future alert types (e.g., DC-153 card ordering).
 */
export function DashboardAlerts() {
  const { t } = useTranslation('dashboard')
  const searchParams = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()
  const cleanedRef = useRef(false)

  const addressUpdated = searchParams.get('addressUpdated') === 'true'
  const cardsRequested = searchParams.get('cardsRequested') === 'true'
  const hasAlertParams = addressUpdated || cardsRequested

  useEffect(() => {
    if (hasAlertParams && !cleanedRef.current) {
      cleanedRef.current = true
      router.replace(pathname, { scroll: false })
    }
  }, [hasAlertParams, router, pathname])

  if (!hasAlertParams) {
    return null
  }

  return (
    <div className="margin-bottom-3">
      {addressUpdated && !cardsRequested && (
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

      {addressUpdated && cardsRequested && (
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
