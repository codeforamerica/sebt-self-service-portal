'use client'

import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

export function EnrolledSection({ results }: { results: ChildCheckApiResponse[] }) {
  const { t } = useTranslation('result')
  if (results.length === 0) return null

  return (
    <section>
      <h2 className="font-family-sans">{t('enrolledHeading')}</h2>
      {results.map(child => (
        <ChildResultCard
          key={child.checkId}
          firstName={child.firstName}
          lastName={child.lastName}
          displayStatus="enrolled"
        />
      ))}
    </section>
  )
}
