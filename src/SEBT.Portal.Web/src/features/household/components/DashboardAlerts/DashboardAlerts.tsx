'use client'

import { Alert } from '@sebt/design-system'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { useHouseholdData } from '@/features/household'

/**
 * Displays success and warning alerts on the dashboard triggered by URL search params.
 * Captures alert state on first read, then cleans the params from the URL.
 * The alert persists because rendering is driven by captured state, not live params.
 * Card replacement success (flash=card_replaced) checks the household data cache
 * for address presence to tailor the alert body, avoiding PII in URL params.
 */
export function DashboardAlerts() {
  const { t } = useTranslation('dashboard')

  const searchParams = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()
  const { data: householdData } = useHouseholdData()

  // Capture alert state from URL params on first read so the alert
  // survives the URL cleanup that follows.
  const [alerts] = useState(() => ({
    addressUpdated: searchParams.get('addressUpdated') === 'true',
    cardsRequested: searchParams.get('cardsRequested') === 'true',
    cardReplaced: searchParams.get('flash') === 'card_replaced',
    addressUpdateFailed: searchParams.get('addressUpdateFailed') === 'true',
    contactUpdateFailed: searchParams.get('contactUpdateFailed') === 'true',
    // TODO: Determine trigger logic — possibly driven by household data (e.g., address
    // hasn't been confirmed in N months, or address on file doesn't match state records).
    addressVerification: searchParams.get('addressVerification') === 'true'
  }))

  const hasAlerts =
    alerts.addressUpdated ||
    alerts.cardsRequested ||
    alerts.cardReplaced ||
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
          heading={t('alertAddressUpdated')}
        >
          {t('alertAddressUpdatedBody', 'Your address update has been recorded.')}
        </Alert>
      )}

      {alerts.addressUpdated && alerts.cardsRequested && (
        <Alert
          variant="success"
          heading={t('alertCardsRequestedHeading', 'Address update and card replacement recorded')}
        >
          {t(
            'alertCardsRequestedBody',
            'Your address update and card replacement request have been recorded.'
          )}
        </Alert>
      )}

      {alerts.cardReplaced && (
        <Alert
          variant="success"
          // TODO update copy for alertCardReplacedHeading
          heading={t('alertCardReplacedHeading', 'Your replacement card request has been recorded')}
        >
          {/* TODO update copy for alertCardReplacedBody (no-address branch) */}
          {householdData?.addressOnFile
            ? t('alertAddressBody')
            : t(
                'alertCardReplacedBody',
                'New cards usually arrive in your mailbox within 7-10 business days.'
              )}
        </Alert>
      )}

      {/* Warning alerts per CO-05 mockup — yellow with dark yellow left border.
          TODO: Wire to actual error flows once state connector persistence is integrated.
          Currently triggered by URL params for visual verification. */}

      {alerts.addressUpdateFailed && (
        // TODO update copy
        <Alert
          variant="warning"
          heading={t('alertAddressUpdateError')}
        >
          {t(
            'alertAddressUpdateFailedBody',
            'Please try again later or contact the Summer EBT Help Desk for assistance.'
          )}
        </Alert>
      )}

      {alerts.contactUpdateFailed && (
        // TODO update copy
        <Alert
          variant="warning"
          heading={t('alertContactUpdateError')}
        >
          {t('alertContactUpdateError')}
        </Alert>
      )}

      {alerts.addressVerification && (
        <Alert
          variant="warning"
          heading={t('alertCheckAddressTitle')}
        >
          {t('alertCheckAddressBody')}
        </Alert>
      )}
    </div>
  )
}
