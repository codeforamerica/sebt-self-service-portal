'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { Button } from '@/components/ui'
import { useHouseholdData } from '@/features/household'

const FIS_PHONE = '(888) 304-9167'
const FIS_PHONE_HREF = 'tel:+18883049167'

// TODO: Routing logic for when co-loaded users reach this screen.
// Currently reachable at /profile/address/info but no automatic redirect exists.
// See D9 and questions.md for co-loaded status data source.

// TODO: Integration with card flow — DC-153 may route co-loaded users here
// from the card replacement flow as well.

/**
 * DC-01: Informational screen for co-loaded DC users.
 * Tells them to call FIS to request a replacement card.
 * This screen is DC-only and lives outside the address update flow layout.
 */
export function CoLoadedInfo() {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { data } = useHouseholdData()

  const address = data?.addressOnFile

  return (
    <div>
      <p>
        {t(
          'coLoadedFisCallout',
          `Call Fidelity Information Services (FIS) at ${FIS_PHONE} to request a replacement card to be sent to your address listed here.`
        )}
      </p>

      {address && (
        <div className="margin-y-2">
          {address.streetAddress1 && (
            <p className="text-bold margin-bottom-05">{address.streetAddress1}</p>
          )}
          {address.streetAddress2 && (
            <p className="text-bold margin-bottom-05">{address.streetAddress2}</p>
          )}
          <p className="text-bold">
            {[address.city, address.state].filter(Boolean).join(', ')} {address.postalCode}
          </p>
        </div>
      )}

      <p>
        {t(
          'coLoadedFisProcess',
          'If FIS has your current, correct address, they will process your request directly.'
        )}
      </p>

      <p className="text-bold">
        {t(
          'coLoadedKeepCard',
          'Keep your card for next year. Benefits will be added to your new card.'
        )}
      </p>

      <p>
        <a
          href={FIS_PHONE_HREF}
          className="usa-link"
        >
          {t('coLoadedTapToCall', 'Tap to call Fidelity Information Services (FIS)')}
        </a>
      </p>

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={() => router.back()}
        >
          {tCommon('back', 'Back')}
        </Button>
        <Button
          type="button"
          onClick={() => router.push('/dashboard')}
        >
          {tCommon('continue', 'Continue')}
        </Button>
      </div>
    </div>
  )
}
