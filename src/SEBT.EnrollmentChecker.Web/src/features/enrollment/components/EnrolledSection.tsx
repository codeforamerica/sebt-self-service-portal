'use client'

import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

export function EnrolledSection({ results }: { results: ChildCheckApiResponse[] }) {
  const { t } = useTranslation('result')
  if (results.length === 0) return null

  return (
    <section data-testid="enrolled-summary-box">
      <h3 className="usa-summary-box__heading">{t('streamlinedEnrolledBody1')}</h3>
      <ul>
        {results.map((child) => (
          <ChildResultCard
            key={child.checkId}
            firstName={child.firstName}
            lastName={child.lastName}
            displayStatus="enrolled"
          />
        ))}
      </ul>
    </section>
  )
}
