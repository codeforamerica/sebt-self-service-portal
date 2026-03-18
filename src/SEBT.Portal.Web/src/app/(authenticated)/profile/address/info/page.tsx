'use client'

import { useTranslation } from 'react-i18next'

import { CoLoadedInfo } from '@/features/address/components/CoLoadedInfo'

export default function CoLoadedInfoPage() {
  const { t } = useTranslation('confirmInfo')

  return (
    <div className="grid-container maxw-tablet">
      <h1>{t('coLoadedInfoTitle', 'A few things to know before replacing cards')}</h1>
      <CoLoadedInfo />
    </div>
  )
}
