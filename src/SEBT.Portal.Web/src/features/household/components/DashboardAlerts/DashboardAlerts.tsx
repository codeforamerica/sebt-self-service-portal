'use client'

import { Alert } from '@/components/ui'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'

/**
 * Displays success and warning alerts on the dashboard triggered by URL search params.
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
    cardsRequested: searchParams.get('cardsRequested') === 'true',
    addressUpdateFailed: searchParams.get('addressUpdateFailed') === 'true',
    contactUpdateFailed: searchParams.get('contactUpdateFailed') === 'true',
    // TODO: Determine trigger logic — possibly driven by household data (e.g., address
    // hasn't been confirmed in N months, or address on file doesn't match state records).
    addressVerification: searchParams.get('addressVerification') === 'true'
  }))

  const hasAlerts =
    alerts.addressUpdated ||
    alerts.cardsRequested ||
    alerts.addressUpdateFailed ||
    alerts.contactUpdateFailed ||
    alerts.addressVerification

  useEffect(() => {
    if (hasAlerts) {
      router.replace(pathname, { scroll: false })
    }
  }, [hasAlerts, router, pathname])

  if (!hasAlerts) {
    return null
  }

  return (
    <div className="margin-bottom-3 display-flex flex-column gap-2">
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

      {/* Warning alerts per CO-05 mockup — yellow with dark yellow left border.
          TODO: Wire to actual error flows once state connector persistence is integrated.
          Currently triggered by URL params for visual verification. */}

      {alerts.addressUpdateFailed && (
        <Alert
          variant="warning"
          // TODO: Use t('addressUpdateFailedHeading') once key is in CSV
          heading="There was an issue updating your mailing address."
        >
          {/* TODO: Use t('addressUpdateFailedBody') once key is in CSV */}
          Please try again later or contact the Summer EBT Help Desk for assistance.
        </Alert>
      )}

      {alerts.contactUpdateFailed && (
        <Alert
          variant="warning"
          // TODO: Use t('contactUpdateFailedHeading') once key is in CSV
          heading="There was an issue updating your contact preferences."
        >
          {/* TODO: Use t('contactUpdateFailedBody') once key is in CSV */}
          Please try again later.
        </Alert>
      )}

      {alerts.addressVerification && (
        <Alert
          variant="warning"
          // TODO: Use t('addressVerificationHeading') once key is in CSV
          heading="Is your address correct?"
        >
          {/* TODO: Use t('addressVerificationBody') once key is in CSV */}
          Please verify your mailing address is up to date so you can receive your Summer EBT cards.
        </Alert>
      )}
    </div>
  )
}
