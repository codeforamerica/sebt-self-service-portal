'use client'

import { TextLink } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'

interface NotEnrolledSectionProps {
  children: ChildCheckApiResponse[]
  applicationUrl: string
}

export function NotEnrolledSection({ children, applicationUrl }: NotEnrolledSectionProps) {
  const { t } = useTranslation('result')
  if (children.length === 0) return null

  return (
    <section>
      <h2 className="font-family-sans">{t('notEnrolledHeading')}</h2>
      {children.map(child => (
        <ChildResultCard
          key={child.checkId}
          firstName={child.firstName}
          lastName={child.lastName}
          displayStatus="notEnrolled"
        />
      ))}
      <p className="usa-prose">
        {t('notEnrolledCta')}{' '}
        <TextLink href={applicationUrl}>{t('applyLink')}</TextLink>
      </p>
    </section>
  )
}
