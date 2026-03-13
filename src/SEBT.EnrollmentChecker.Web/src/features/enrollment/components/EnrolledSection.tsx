'use client'

import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

export function EnrolledSection({ children }: { children: ChildCheckApiResponse[] }) {
  const { t } = useTranslation('result')
  if (children.length === 0) return null

  return (
    <section>
      <h2>{t('enrolledHeading')}</h2>
      {children.map(child => (
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
