'use client'

import { Alert, Button, getState } from '@sebt/design-system'
import Link from 'next/link'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { clearReplacementFlash, getReplacementFlash } from '@/features/cards/utils/replacementFlash'
import { useHouseholdData } from '@/features/household'

import type { Address } from '../../api'
import { fillReplacementHeading } from './replacementHeading'

/** Renders a mailing address as stacked lines. */
function AddressLines({ address }: { address: Address }) {
  return (
    <span className="display-block margin-top-1">
      {address.streetAddress1 && <span className="display-block">{address.streetAddress1}</span>}
      {address.streetAddress2 && <span className="display-block">{address.streetAddress2}</span>}
      {(address.city || address.state || address.postalCode) && (
        <span className="display-block">
          {address.city}, {address.state} {address.postalCode}
        </span>
      )}
    </span>
  )
}

/**
 * Displays success and warning alerts on the dashboard triggered by URL search params.
 * Captures alert state on first read, then cleans the params from the URL so the alert
 * persists (rendering is driven by captured state, not live params). The card-replacement
 * success body reads the household cache for the address to avoid PII in URL params.
 *
 * Triggers are URL params today. The address-verification ("check your mailing address")
 * alert will eventually be driven by a backend returned-mail signal; until that data
 * exists, any producer can deep-link to /dashboard?addressVerification=true.
 */
export function DashboardAlerts() {
  const { t, i18n } = useTranslation('dashboard')

  const searchParams = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()
  const { data: householdData } = useHouseholdData()

  // Capture alert state from URL params on first read so the alert survives the
  // URL cleanup that follows.
  const [alerts] = useState(() => ({
    addressUpdated: searchParams.get('addressUpdated') === 'true',
    cardReplaced: searchParams.get('flash') === 'card_replaced',
    contactUpdated: searchParams.get('contactUpdated') === 'true',
    addressUpdateFailed: searchParams.get('addressUpdateFailed') === 'true',
    contactUpdateFailed: searchParams.get('contactUpdateFailed') === 'true',
    addressVerification: searchParams.get('addressVerification') === 'true'
  }))

  // Replaced-card details are handed off in memory by the confirm screen (they
  // are PII, so they never ride URL params or web storage). Read on first
  // render, clear once mounted.
  const [replacedCards] = useState(() => getReplacementFlash())
  useEffect(() => {
    clearReplacementFlash()
  }, [])

  // "Yes, this is my address" dismisses the check-address prompt in place. There is no
  // backend acknowledgment yet, so this is local-only.
  const [addressVerifyDismissed, setAddressVerifyDismissed] = useState(false)

  const hasAlerts =
    alerts.addressUpdated ||
    alerts.cardReplaced ||
    alerts.contactUpdated ||
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

  // CO identifies cards by their last four digits, DC by the child's name; the
  // per-state alertAddressTitle template expects the matching list. Resolved in
  // the render path so the heading follows a language switch.
  const replacedItems =
    getState() === 'co'
      ? replacedCards.map((c) => c.ebtCardLastFour).filter((d): d is string => Boolean(d))
      : replacedCards.map((c) => `${c.childFirstName} ${c.childLastName}`)
  const cardReplacedHeading = alerts.cardReplaced
    ? fillReplacementHeading(t('alertAddressTitle'), replacedItems, i18n.language)
    : null

  return (
    <div className="margin-bottom-3 display-flex flex-column gap-2">
      {alerts.addressUpdated && <Alert variant="success">{t('alertAddressUpdated')}</Alert>}

      {alerts.cardReplaced && (
        <Alert
          variant="success"
          {...(cardReplacedHeading !== null ? { heading: cardReplacedHeading } : {})}
        >
          {householdData?.addressOnFile && <AddressLines address={householdData.addressOnFile} />}
          {t('alertAddressBody')}
        </Alert>
      )}

      {alerts.contactUpdated && <Alert variant="success">{t('alertContactUpdated')}</Alert>}

      {alerts.addressUpdateFailed && (
        <Alert variant="warning">{t('alertAddressUpdateError')}</Alert>
      )}

      {alerts.contactUpdateFailed && (
        <Alert variant="warning">{t('alertContactUpdateError')}</Alert>
      )}

      {alerts.addressVerification && !addressVerifyDismissed && (
        <Alert
          variant="warning"
          heading={t('alertCheckAddressTitle')}
        >
          {t('alertCheckAddressBody')}
          {householdData?.addressOnFile && <AddressLines address={householdData.addressOnFile} />}
          <Button
            variant="unstyled"
            className="display-block margin-top-2"
            onClick={() => setAddressVerifyDismissed(true)}
          >
            {t('alertCheckAddressLink1')}
          </Button>
          <Link
            href="/profile/address"
            className="usa-link display-block margin-top-1"
          >
            {t('alertCheckAddressLink2')}
          </Link>
        </Alert>
      )}
    </div>
  )
}
