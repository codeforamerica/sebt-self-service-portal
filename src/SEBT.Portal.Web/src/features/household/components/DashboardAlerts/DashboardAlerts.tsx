'use client'

import { Alert } from '@/components/ui'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'

/**
 * Displays success alerts on the dashboard triggered by URL search params.
 * Captures alert state on first read, then cleans the params from the URL.
 * The alert persists because rendering is driven by captured state, not live params.
 * Extensible: add new param checks for future alert types (e.g., DC-153 card ordering).
 */
export function DashboardAlerts() {
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
          // TODO: Use t('addressUpdatedHeading') once real persistence is wired up
          heading="Address update recorded"
        >
          {/* TODO: Use t('addressUpdatedBody') once real persistence is wired up */}
          Your address update has been recorded. State system integration is pending — changes are
          not yet reflected in the benefits system.
        </Alert>
      )}

      {alerts.addressUpdated && alerts.cardsRequested && (
        <Alert
          variant="success"
          // TODO: Use t('cardsRequestedHeading') once real persistence is wired up
          heading="Address update and card replacement recorded"
        >
          {/* TODO: Use t('cardsRequestedBody') once real persistence is wired up */}
          Your address update and card replacement request have been recorded. State system
          integration is pending — changes are not yet reflected in the benefits system.
        </Alert>
      )}
    </div>
  )
}
