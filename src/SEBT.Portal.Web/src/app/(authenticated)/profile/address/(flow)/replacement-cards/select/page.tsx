'use client'

import { useTranslation } from 'react-i18next'

export default function CardSelectionPage() {
  const { t } = useTranslation('confirmInfo')

  return (
    <div className="grid-container maxw-tablet">
      <h1>{t('cardSelectionTitle', 'Which cards need to be replaced?')}</h1>
      <p>{t('cardSelectionIntro', 'Select the cards you would like to replace.')}</p>
      {/* CardSelection component will be wired up in Step 6 */}
    </div>
  )
}
