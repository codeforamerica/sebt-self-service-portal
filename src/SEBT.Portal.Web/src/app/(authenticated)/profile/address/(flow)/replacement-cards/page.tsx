'use client'

import { useTranslation } from 'react-i18next'

import { useAddressFlow } from '@/features/address'
import { ReplacementCardPrompt } from '@/features/address/components/ReplacementCardPrompt'
import { getState, getStateConfig } from '@/lib/state'

export default function ReplacementCardsPage() {
  const { t } = useTranslation('confirmInfo')
  const { address } = useAddressFlow()
  const { programName } = getStateConfig(getState())

  // Flow layout guards against missing address and redirects to the form (D4).
  if (!address) {
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
        {t(
          'replacementCardsTitle',
          `Do you want to request replacement ${programName} cards to be sent to this address?`
        )}
      </h1>
      <ReplacementCardPrompt address={address} />
    </div>
  )
}
