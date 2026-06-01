'use client'

import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

interface NotEnrolledSectionProps {
  results: ChildCheckApiResponse[]
}

export function NotEnrolledSection({ results }: NotEnrolledSectionProps) {
  const { t } = useTranslation('result')
  if (results.length === 0) return null

  return (
    <section data-testid="not-enrolled-summary-box">
      <h4 className="usa-summary-box__heading">{t('applyForSebtBody1')}</h4>
      <div className="usa-summary-box__text">
        <ul>
        {results.map(child => (
          <ChildResultCard
            key={child.checkId}
            firstName={child.firstName}
            lastName={child.lastName}
            displayStatus="notEnrolled"
          />
        ))}
        </ul>
      </div>
    </section>
  )
}
