'use client'

import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

export function EnrolledSection({ results }: { results: ChildCheckApiResponse[] }) {
  const { t } = useTranslation('result')
  if (results.length === 0) return null

  return (
    <section data-testid="enrolled-summary-box">
      <h4 className="usa-summary-box__heading">{t('streamlinedEnrolledBody1')}</h4>
      <div className="usa-summary-box__text">
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
      </div>
    </section>
  )
}
