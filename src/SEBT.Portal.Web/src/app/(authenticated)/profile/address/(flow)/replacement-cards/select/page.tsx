'use client'

import { useTranslation } from 'react-i18next'

import { CardSelection } from '@/features/address/components/CardSelection'

export default function CardSelectionPage() {
  const { t } = useTranslation('confirmInfo')

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('cardSelectionTitle', 'Which cards need to be replaced?')}
      </h1>
      <CardSelection />
    </div>
  )
}
