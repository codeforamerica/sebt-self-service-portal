'use client'

import { useTranslation } from 'react-i18next'

import { AddressForm } from '@/features/address/components/AddressForm'

// TODO (D9): Eligibility check — redirect co-loaded DC users to /profile/address/info.
// Canonical data source for co-loaded status is TBD (see questions.md).
// Will need useHouseholdData() + getState() to check benefitIssuanceType.

export default function AddressFormPage() {
  const { t } = useTranslation('confirmInfo')

  return (
    <div className="grid-container maxw-tablet">
      <h1>{t('pageTitle', 'Tell us where to safely send your mail')}</h1>
      <p className="usa-hint">
        {t('requiredFieldsNote', 'Asterisks (*) indicate a required field')}
      </p>
      <AddressForm />
    </div>
  )
}
