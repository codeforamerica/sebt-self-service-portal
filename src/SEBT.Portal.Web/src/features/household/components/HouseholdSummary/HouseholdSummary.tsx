'use client'

import Link from 'next/link'
import { useTranslation } from 'react-i18next'

import type { Address, HouseholdData } from '../../api'
import { useRequiredHouseholdData } from '../../api'

function formatAddress(address: Address): string {
  const parts = [
    address.streetAddress1,
    address.streetAddress2,
    [address.city, address.state, address.postalCode].filter(Boolean).join(', ')
  ].filter(Boolean)

  return parts.join('\n')
}

// Keys map to CSV: "S2 - Portal Dashboard - Profile Table - Status {Status}"
function getOverallStatus(data: HouseholdData): {
  labelKey: string
  fallback: string
  variant: 'success' | 'warning' | 'error' | 'info'
} {
  const statuses = data.applications.map((app) => app.applicationStatus)

  if (statuses.includes('Approved')) {
    return { labelKey: 'profileTableStatusEnrolled', fallback: 'Enrolled', variant: 'success' }
  }
  if (statuses.includes('Denied')) {
    return {
      labelKey: 'profileTableStatusApplicationDenied',
      fallback: 'Application denied',
      variant: 'error'
    }
  }
  if (statuses.includes('Pending') || statuses.includes('UnderReview')) {
    return {
      labelKey: 'profileTableStatusApplicationIn-progress',
      fallback: 'Application in-progress',
      variant: 'warning'
    }
  }
  if (statuses.includes('Cancelled')) {
    // TODO: Add CSV row "S2 - Portal Dashboard - Profile Table - Status Cancelled"
    return { labelKey: 'profileTableStatusCancelled', fallback: 'Cancelled', variant: 'info' }
  }
  // TODO: Add CSV row "S2 - Portal Dashboard - Profile Table - Status Unknown"
  return { labelKey: 'profileTableStatusUnknown', fallback: 'Unknown', variant: 'info' }
}

function getStatusTextClass(variant: string): string {
  switch (variant) {
    case 'success':
      return 'text-green'
    case 'error':
      return 'text-red'
    case 'warning':
      return 'text-gold'
    default:
      return 'text-base-dark'
  }
}

// Keys map to CSV: "S2 - Portal Dashboard - Profile Table - {Key}"
export function HouseholdSummary() {
  const { t } = useTranslation('dashboard')
  const data = useRequiredHouseholdData()
  const status = getOverallStatus(data)

  return (
    <div className="usa-card__container margin-bottom-4">
      <div className="usa-card__body">
        <dl className="margin-0">
          {/* Status */}
          <dt className="text-bold">{t('profileTableHeadingStatus')}</dt>
          <dd className="margin-left-0 margin-bottom-2">
            <span className={`text-bold ${getStatusTextClass(status.variant)}`}>
              {t(status.labelKey, status.fallback)}
            </span>
            {status.variant === 'success' && (
              <p className="margin-top-1 margin-bottom-0">
                {t('profileTableStatusEnrolledDescription')}
              </p>
            )}
          </dd>

          {/* Your mailing address */}
          {data.addressOnFile && (
            <>
              <dt className="text-bold">{t('profileTableHeadingAddress')}</dt>
              <dd className="margin-left-0 margin-bottom-2">
                <span style={{ whiteSpace: 'pre-line' }}>{formatAddress(data.addressOnFile)}</span>
                <br />
                <Link
                  href="/profile/address"
                  className="usa-link"
                >
                  {t('profileTableActionChangeAddress')}
                </Link>
              </dd>
            </>
          )}

          {/* Your preferred contact */}
          {(data.email || data.phone) && (
            <>
              <dt className="text-bold">{t('profileTableHeadingContact')}</dt>
              <dd className="margin-left-0 margin-bottom-2">
                {data.email}
                {data.phone && (
                  <>
                    {data.email && <br />}
                    {data.phone}
                  </>
                )}
                <br />
                <Link
                  href="/contact"
                  className="usa-link"
                >
                  {t('profileTableActionChangeContact')}
                </Link>
              </dd>
            </>
          )}
        </dl>
      </div>
    </div>
  )
}
