'use client'

import { useTranslation } from 'react-i18next'

import { CardSelection } from '@/features/address/components/CardSelection'

export default function CardSelectionPage() {
  const { t } = useTranslation('optionalId')

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">{t('title')}</h1>
      <CardSelection />
    </div>
  )
}
