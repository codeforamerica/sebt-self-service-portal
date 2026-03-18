'use client'

import { useTranslation } from 'react-i18next'

import { useAddressFlow } from '@/features/address'
import { ReplacementCardPrompt } from '@/features/address/components/ReplacementCardPrompt'

export default function ReplacementCardsPage() {
  const { t } = useTranslation('confirmInfo')
  const { address } = useAddressFlow()

  // Context-loss guard is handled by the flow layout (D4).
  // If address is null here, the layout will redirect to /profile/address.
  if (!address) {
    return null
  }

  return (
    <div className="grid-container maxw-tablet">
      <h1>
        {t(
          'replacementCardsTitle',
          'Do you want to request replacement cards to be sent to this address?'
        )}
      </h1>
      <ReplacementCardPrompt address={address} />
    </div>
  )
}
