'use client'

import { useTranslation } from 'react-i18next'

export default function CoLoadedInfoPage() {
  const { t } = useTranslation('confirmInfo')

  return (
    <div className="grid-container maxw-tablet">
      <h1>{t('coLoadedInfoTitle', 'A few things to know')}</h1>
      <p>
        {t(
          'coLoadedInfoBody',
          'This page will contain information for co-loaded users about contacting FIS.'
        )}
      </p>
      {/* CoLoadedInfo component will be wired up in Step 7 */}
    </div>
  )
}
