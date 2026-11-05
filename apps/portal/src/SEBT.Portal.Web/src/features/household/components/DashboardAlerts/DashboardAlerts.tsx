'use client'

import { Alert, Button, getState, getStateConfig } from '@sebt/design-system'
import Link from 'next/link'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

import { clearReplacementFlash, getReplacementFlash } from '@/features/cards/utils/replacementFlash'
import { useHouseholdData } from '@/features/household'

import type { Address } from '../../api'
import { hasUndeliverableCard } from '../../api'
import { fillReplacementHeading } from './replacementHeading'

const ADDRESS_CHECK_DISMISSED_KEY = 'dashboard_addressCheckDismissed'

function readAddressCheckDismissed(): boolean {
  try {
    return sessionStorage.getItem(ADDRESS_CHECK_DISMISSED_KEY) === 'true'
  } catch {
    return false
  }
}

function rememberAddressCheckDismissed(): void {
  try {
    sessionStorage.setItem(ADDRESS_CHECK_DISMISSED_KEY, 'true')
  } catch {
    // Storage unavailable (e.g. private mode): the dismissal still holds until reload.
  }
}

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
 * The address-verification ("Is your address correct?") alert also appears whenever a card
 * is Undeliverable, and can be deep-linked with /dashboard?addressVerification=true.
 * Dismissing it lasts for the browser session.
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

  // No backend acknowledgment exists, so "Yes, this is my address" is remembered per session.
  const [addressVerifyDismissed, setAddressVerifyDismissed] = useState(readAddressCheckDismissed)
  const dismissAddressVerify = () => {
    rememberAddressCheckDismissed()
    setAddressVerifyDismissed(true)
  }

  const showAddressVerification =
    (alerts.addressVerification || hasUndeliverableCard(householdData?.summerEbtCases ?? [])) &&
    !addressVerifyDismissed

  const hasStatusAlerts =
    alerts.addressUpdated ||
    alerts.cardReplaced ||
    alerts.contactUpdated ||
    alerts.addressUpdateFailed ||
    alerts.contactUpdateFailed
  const hasUrlParams = hasStatusAlerts || alerts.addressVerification

  useEffect(() => {
    if (hasUrlParams) {
      router.replace(pathname, { scroll: false })
    }
  }, [hasUrlParams, router, pathname])

  if (!hasStatusAlerts && !showAddressVerification) {
    return null
  }

  // The per-state alertAddressTitle template expects the list that matches the
  // state's cardIdentifier. Resolved in the render path so the heading follows a
  // language switch.
  const replacedItems =
    getStateConfig(getState()).cardIdentifier === 'cardLastFour'
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

      {showAddressVerification && (
        <Alert
          variant="warning"
          heading={t('alertCheckAddressTitle')}
        >
          {householdData?.addressOnFile && <AddressLines address={householdData.addressOnFile} />}
          <span className="display-block margin-top-1">{t('alertCheckAddressBody')}</span>
          <Button
            variant="unstyled"
            className="display-block margin-top-2 text-bold text-ink"
            onClick={dismissAddressVerify}
          >
            {t('alertCheckAddressLink1')}
          </Button>
          <Link
            href="/profile/address"
            className="usa-link display-block margin-top-2 text-bold text-ink"
          >
            {t('alertCheckAddressLink2')}
          </Link>
        </Alert>
      )}
    </div>
  )
}
