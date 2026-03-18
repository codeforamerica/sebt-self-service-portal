'use client'

import { useTranslation } from 'react-i18next'

export default function AddressFormPage() {
  const { t } = useTranslation('confirmInfo')

  // TODO (D9): Eligibility check — redirect co-loaded DC users to /profile/address/info.
  // Canonical data source for co-loaded status is TBD (see questions.md).
  // Will need useHouseholdData() + getState() to check benefitIssuanceType.

  return (
    <div className="grid-container maxw-tablet">
      <h1>{t('pageTitle', 'Update mailing address')}</h1>
      <p>{t('addressFormIntro', 'Update the mailing address for your household.')}</p>
      {/* AddressForm component will be wired up in Step 3 */}
    </div>
  )
}
