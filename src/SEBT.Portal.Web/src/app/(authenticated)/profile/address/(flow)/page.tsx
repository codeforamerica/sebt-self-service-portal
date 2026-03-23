'use client'

import { useTranslation } from 'react-i18next'

import { AddressForm } from '@/features/address/components/AddressForm'
import { useHouseholdData } from '@/features/household'

// TODO (D9): Eligibility check — redirect co-loaded DC users to /profile/address/info.
// Canonical data source for co-loaded status is TBD (see questions.md).
// Will need getState() to check benefitIssuanceType.

// TODO (DC-153): Card-flow entry point — when accessed via /profile/address?from=cards,
// the form should return the user to the card replacement flow on completion
// instead of the replacement card prompt. See DC-02/CO-01 mockups.

export default function AddressFormPage() {
  const { t } = useTranslation('confirmInfo')
  const { data, isLoading } = useHouseholdData()

  if (isLoading) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading…</span>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('pageTitle', 'Tell us where to safely send your mail')}
      </h1>
      <p className="usa-hint">
        {t('requiredFieldsNote', 'Asterisks (*) indicate a required field')}
      </p>
      <AddressForm initialAddress={data?.addressOnFile ?? null} />
    </div>
  )
}
