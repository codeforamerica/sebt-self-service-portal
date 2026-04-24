'use client'

import { LinkItem } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

interface NotEnrolledSectionProps {
  results: ChildCheckApiResponse[]
  applicationUrl: string
}

export function NotEnrolledSection({ results, applicationUrl }: NotEnrolledSectionProps) {
  const { t } = useTranslation('result')
  if (results.length === 0) return null

  return (
    <section>
      <h2 className="font-family-sans">{t('applyForSebtBody1')}</h2>
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

      {/* TODO should this open in new window? */}
      <p className="usa-prose">
        {t('applyForSebtBody')}{' '}
        <a
          href={applicationUrl}
          data-analytics-cta="apply_cta"
          className="usa-button"
        >
          {t('applyLink')}
        </a>
      </p>
    </section>
  )
}
