'use client'

import { Button, RichText } from '@sebt/design-system'
import { useRouter } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { useEnrollment } from '../context/EnrollmentContext'

/**
 * Content key prefix for this outcome's copy. The enrolled and not-enrolled
 * results ask the same question in different words, and the sheet stores each
 * under its own prefix.
 */
export type CheckAnotherChildCopy = 'streamlinedEnrolledCard2' | 'applyForSebtCard2'

interface CheckAnotherChildCardProps {
  copy: CheckAnotherChildCopy
}

/**
 * Returns a single-child flow to the form for the next child.
 *
 * The finished child is dropped on the way out: the next check stands alone, so
 * carrying the previous one forward would both widen the submission and leave
 * personal details in session storage that the visitor can no longer see or
 * remove.
 */
export function CheckAnotherChildCard({ copy }: CheckAnotherChildCardProps) {
  const { t } = useTranslation('result')
  const router = useRouter()
  const { clearState } = useEnrollment()

  function handleClick() {
    clearState()
    router.push('/check')
  }

  return (
    <section
      className="usa-summary-box margin-top-4"
      data-testid="check-another-child"
    >
      <h2 className="usa-summary-box__heading">{t(`${copy}Title`)}</h2>
      <div className="usa-summary-box__text">
        <RichText>{t(`${copy}Body`)}</RichText>
        <p className="margin-top-2">
          <Button
            variant="outline"
            onClick={handleClick}
            data-analytics-cta="check_another_child_cta"
          >
            {t(`${copy}Action`)}
          </Button>
        </p>
      </div>
    </section>
  )
}
