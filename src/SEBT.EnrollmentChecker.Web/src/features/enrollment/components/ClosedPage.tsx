'use client'

import { useTranslation } from 'react-i18next'

export function ClosedPage() {
  // Keys 'closed.title' and 'closed.body' must be added to the checker CSV
  // once content is finalized. See spec: "Content gap — /closed page"
  const { t } = useTranslation('checker')
  return (
    <div className="usa-section">
      <div className="grid-container">
        <h1 className="font-family-sans">{t('closed.title')}</h1>
        <p>{t('closed.body')}</p>
      </div>
    </div>
  )
}
