'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { useHouseholdData } from '@/features/household'
import { Button } from '@sebt/design-system'

const FIS_PHONE = '(888) 304-9167'
const FIS_PHONE_HREF = 'tel:+18883049167'

type CoLoadedInfoVariant = 'card' | 'address'

interface CoLoadedInfoProps {
  /**
   * Selects which intent's copy to render.
   * - 'card' (default): guidance for requesting a replacement card via FIS
   * - 'address': guidance for updating mailing address via FIS
   */
  variant?: CoLoadedInfoVariant
  /**
   * When true, render a single "Return to dashboard" action instead of the
   * Back + Continue pair. Used when this screen is a terminal destination
   * (e.g. denied-user redirect to /profile/address/info) and there is no
   * next step in a surrounding flow.
   */
  terminal?: boolean
}

/**
 * DC-01: Informational screen for co-loaded DC users.
 * Tells them to call FIS. Copy variant switches between the "replacement card"
 * and "address update" intents; both paths converge on the same FIS contact.
 */
export function CoLoadedInfo({ variant = 'card', terminal = false }: CoLoadedInfoProps = {}) {
  const { t } = useTranslation('confirmInfo')
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()
  const { data } = useHouseholdData()

  const address = data?.addressOnFile

  return (
    <div>
      {variant === 'address' ? (
        <p>
          {/* TODO: Remove fallback once coLoadedFisAddressCallout is added to CSV */}
          {t(
            'coLoadedFisAddressCallout',
            `Call Fidelity Information Services (FIS) at ${FIS_PHONE} to update the mailing address for your SNAP or TANF EBT card.`
          )}
        </p>
      ) : (
        <p>
          {t(
            'coLoadedFisCallout',
            `Call Fidelity Information Services (FIS) at ${FIS_PHONE} to request a replacement card to be sent to your address listed here.`
          )}
        </p>
      )}

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

      {variant === 'card' && (
        <p className="text-bold">
          {t(
            'coLoadedKeepCard',
            'Keep your card for next year. Benefits will be added to your new card.'
          )}
        </p>
      )}

      <p>
        <a
          href={FIS_PHONE_HREF}
          className="usa-link"
        >
          {t('coLoadedTapToCall', 'Tap to call Fidelity Information Services (FIS)')}
        </a>
      </p>

      <div className="margin-top-3 display-flex flex-row gap-2">
        {terminal ? (
          <Button
            type="button"
            onClick={() => router.push('/dashboard')}
          >
            {tCommon('profileAddressBackToDashboard', 'Return to dashboard')}
          </Button>
        ) : (
          <>
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
          </>
        )}
      </div>
    </div>
  )
}
