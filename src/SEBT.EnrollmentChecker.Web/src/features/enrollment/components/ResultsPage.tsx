'use client'

import { useRouter } from 'next/navigation'
import Image from 'next/image'
import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'
import { mapApiStatus } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'
import { EnrolledSection } from './EnrolledSection'
import { NotEnrolledSection } from './NotEnrolledSection'

interface ResultsPageProps {
  results: ChildCheckApiResponse[]
  applicationUrl: string
}

export function ResultsPage({ results, applicationUrl }: ResultsPageProps) {
  const { t } = useTranslation('result')
  const router = useRouter()

  const enrolled = results.filter(r => mapApiStatus(r.status) === 'enrolled')
  const notEnrolled = results.filter(r => mapApiStatus(r.status) === 'notEnrolled')
  const errors = results.filter(r => mapApiStatus(r.status) === 'error')

  return (
    <div className="usa-section">
      <div className="grid-container">
        <Image
          src="/images/states/co/icon-review-card.svg"
          alt=""
          width={100}
          height={75}
          aria-hidden="true"
        />
        <h1 className="font-family-sans margin-top-1">{t('title')}</h1>
        <EnrolledSection results={enrolled} />
        <NotEnrolledSection results={notEnrolled} applicationUrl={applicationUrl} />
        {errors.length > 0 && (
          <section>
            <h2 className="font-family-sans">{t('errorTitle')}</h2>
            {errors.map(child => (
              <ChildResultCard
                key={child.checkId}
                firstName={child.firstName}
                lastName={child.lastName}
                displayStatus="error"
                {...(child.statusMessage !== undefined && { errorMessage: child.statusMessage })}
              />
            ))}
          </section>
        )}
      </div>
    </div>
  )
}
