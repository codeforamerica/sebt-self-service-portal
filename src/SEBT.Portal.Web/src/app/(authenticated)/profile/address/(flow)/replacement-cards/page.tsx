'use client'

import { useTranslation } from 'react-i18next'

export default function ReplacementCardsPage() {
  const { t } = useTranslation('confirmInfo')

  return (
    <div className="grid-container maxw-tablet">
      <h1>{t('replacementCardsTitle', 'Request replacement cards')}</h1>
      <p>
        {t(
          'replacementCardsIntro',
          'Would you like to request replacement cards with your new address?'
        )}
      </p>
      {/* ReplacementCardPrompt component will be wired up in Step 5 */}
    </div>
  )
}
