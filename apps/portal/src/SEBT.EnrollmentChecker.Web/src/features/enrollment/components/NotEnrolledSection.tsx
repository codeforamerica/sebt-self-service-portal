'use client'

import { RichText } from '@sebt/design-system'
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
      <h4 className="usa-summary-box__heading">
        <RichText inline>{t('applyForSebtBody1')}</RichText>
      </h4>
      <div className="usa-summary-box__text">
        <ul>
          {results.map((child) => (
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
