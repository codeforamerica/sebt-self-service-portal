'use client'

import { Alert } from '@sebt/design-system'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * Displays success and warning alerts on the dashboard triggered by URL search params.
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
          heading={t('alertAddressUpdatedHeading', 'Address update recorded')}
        >
          {t(
            'alertAddressUpdatedBody',
            'Your address update has been recorded. State system integration is pending — changes are not yet reflected in the benefits system.'
          )}
        </Alert>
      )}

      {alerts.addressUpdated && alerts.cardsRequested && (
        <Alert
          variant="success"
          heading={t('alertCardsRequestedHeading', 'Address update and card replacement recorded')}
        >
          {t(
            'alertCardsRequestedBody',
            'Your address update and card replacement request have been recorded. State system integration is pending — changes are not yet reflected in the benefits system.'
          )}
        </Alert>
      )}

      {/* Warning alerts per CO-05 mockup — yellow with dark yellow left border.
          TODO: Wire to actual error flows once state connector persistence is integrated.
          Currently triggered by URL params for visual verification. */}

      {alerts.addressUpdateFailed && (
        <Alert
          variant="warning"
          heading={t(
            'alertAddressUpdateFailedHeading',
            'There was an issue updating your mailing address.'
          )}
        >
          {t(
            'alertAddressUpdateFailedBody',
            'Please try again later or contact the Summer EBT Help Desk for assistance.'
          )}
        </Alert>
      )}

      {alerts.contactUpdateFailed && (
        <Alert
          variant="warning"
          heading={t(
            'alertContactUpdateFailedHeading',
            'There was an issue updating your contact preferences.'
          )}
        >
          {t('alertContactUpdateFailedBody', 'Please try again later.')}
        </Alert>
      )}

      {alerts.addressVerification && (
        <Alert
          variant="warning"
          heading={t('alertAddressVerificationHeading', 'Is your address correct?')}
        >
          {t(
            'alertAddressVerificationBody',
            'Please verify your mailing address is up to date so you can receive your Summer EBT cards.'
          )}
        </Alert>
      )}
    </div>
  )
}
