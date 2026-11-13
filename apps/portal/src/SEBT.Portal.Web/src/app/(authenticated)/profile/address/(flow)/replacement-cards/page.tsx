'use client'

import { useTranslation } from 'react-i18next'

import { useAddressFlow } from '@/features/address'
import { ReplacementCardPrompt } from '@/features/address/components/ReplacementCardPrompt'
import { PageHeading } from '@sebt/design-system'

export default function ReplacementCardsPage() {
  const { t } = useTranslation('confirmInfo')
  const { t: tDev } = useTranslation('dev')

  const { address } = useAddressFlow()

  // Flow layout guards against missing address and redirects to the form (D4).
  if (!address) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">{tDev('loading')}</span>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <PageHeading className="font-sans-xl text-primary">{t('replacementCardsTitle')}</PageHeading>
      <ReplacementCardPrompt address={address} />
    </div>
  )
}
