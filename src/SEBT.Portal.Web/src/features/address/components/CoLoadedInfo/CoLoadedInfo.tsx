'use client'

import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { Button } from '@sebt/design-system'

interface CoLoadedInfoProps {
  /**
   * Whether to show the Continue button. Defaults to false.
   * The standalone /cards/info page is a dead-end per DC-01 and shows only Back.
   */
  showContinue?: boolean
}

/**
 * DC-01: Informational screen for co-loaded DC users.
 * Tells them to visit a DHS EBT Card Office to replace their SNAP/TANF card.
 * This screen is DC-only and lives outside the address update flow layout.
 */
export function CoLoadedInfo({ showContinue = false }: CoLoadedInfoProps) {
  const { t: tCommon } = useTranslation('common')
  const router = useRouter()

  return (
    <div>
      <p>
        {/* TODO: Use t('coLoadedDhsCardOffice') once key is available in CSV */}
        You can get a replacement SNAP or TANF EBT card at a DHS EBT Card Office.
      </p>

      <p>
        {/* TODO: Use t('coLoadedBenefitsAdded') once key is available in CSV */}
        Your DC SUN Bucks benefits will be added to your regular SNAP or TANF benefits.
      </p>

      <p>
        {/* TODO: Use t('coLoadedOfficeHours') once key is available in CSV */}
        Offices are open Monday through Friday, from 7:30 a.m. to 4:45 p.m.
      </p>

      <p className="text-bold">
        {/* TODO: Use t('coLoadedOfficeLocationsHeading') once key is available in CSV */}
        DHS EBT Card Office locations:
      </p>

      <ul className="usa-list margin-top-05">
        <li>645 H Street NE, 2nd Floor</li>
        <li>1849 Marion Barry Avenue SE</li>
      </ul>

      <div className="margin-top-3 display-flex flex-row gap-2">
        <Button
          variant="outline"
          type="button"
          onClick={() => router.back()}
        >
          {tCommon('back', 'Back')}
        </Button>
        {showContinue && (
          <Button
            type="button"
            onClick={() => router.push('/dashboard')}
          >
            {tCommon('continue', 'Continue')}
          </Button>
        )}
      </div>
    </div>
  )
}
