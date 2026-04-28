'use client'

import Image from 'next/image'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
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

type HouseholdEnrollmentResult = 'allEnrolled' | 'noneEnrolled' | 'mixedEnrolled' | 'indeterminate'

function computeHouseholdEnrollmentResult(
  enrolledCount: number,
  notEnrolledCount: number
): HouseholdEnrollmentResult {
  if (enrolledCount > 0 && notEnrolledCount === 0) {
    return 'allEnrolled'
  } else if (notEnrolledCount > 0 && enrolledCount === 0) {
    return 'noneEnrolled'
  } else if (enrolledCount > 0 && notEnrolledCount > 0) {
    return 'mixedEnrolled'
  } else {
    return 'indeterminate'
  }
}

export function ResultsPage({ results, applicationUrl }: ResultsPageProps) {
  const { t } = useTranslation('result')
  const router = useRouter()
  const [isAccordionExpanded, setIsAccordionExpanded] = useState(false)

  const notEnrolledNextSteps = (
    <section data-testid="not-enrolled-next-steps">
      <h2 className="usa-process-list__heading">{t('applyForSebtActionApply')}</h2>
      <p className="margin-top-05">{t('applyForSebtBody2')}</p>
      <p>
        <a
          href={applicationUrl}
          data-analytics-cta="apply_cta"
          className="usa-button"
          data-testid="apply-for-sebt-link"
        >
          {t('applyLink', 'Continue your application')}
        </a>
      </p>
    </section>
  )

  const enrolledNextSteps = (
    <section data-testid="enrolled-next-steps">
      <h2 className="usa-process-list__heading"> {t('streamlinedEnrolledAlertTitle')}</h2>
      <p className="margin-top-05">{t('streamlinedEnrolledAlertBody')}</p>
      <p>
        <a
          href="#"
          // data-analytics-cta="apply_cta" TODO replace w action for dashbaord
          className="usa-button"
          data-testid="portal-link"
        >
          {t('streamlinedEnrolledAction')}
        </a>
      </p>
    </section>
  )

  const eligibilityAccordion = (
    <div className="usa-accordion margin-top-4" data-testid="eligibility-accordion">
      <h2 className="usa-accordion__heading">
        <button
          type="button"
          className="usa-accordion__button"
          aria-expanded={isAccordionExpanded}
          aria-controls="faq-content"
          onClick={() => setIsAccordionExpanded((prev) => !prev)}
        >
          {t('applyForSebtAccordionTitle')}
        </button>
      </h2>
      <div
        className="usa-accordion__content usa-prose"
        hidden={!isAccordionExpanded}
      >
        <p> {t('applyForSebtAccordionBody1')} </p>

        {/* TO DO: IMPLEMENT CALCULATOR
        <p> {t('applyForSebtAccordionBody2')} </p>
        <p> {t('applyForSebtAccordionBody3')} </p>
        <p> {t('applyForSebtAccordionLabelSelectNumberPeople')} </p>
        <p> {t('applyForSebtBody3')} </p>
        <p> {t('applyForSebtBody4')} </p>
        <p>
          <a
            href={applicationUrl}
            data-analytics-cta="apply_cta"
            className="usa-button"
          >{t('applyLink')}
          </a> 
        </p> 
          </section> */}
      </div>
    </div>
  )

  const enrolled = results.filter((r) => mapApiStatus(r.status) === 'enrolled')
  const notEnrolled = results.filter((r) => mapApiStatus(r.status) === 'notEnrolled')
  const errors = results.filter((r) => mapApiStatus(r.status) === 'error')

  const householdEnrollmentResult = computeHouseholdEnrollmentResult(
    enrolled.length,
    notEnrolled.length
  )

  // const checkmarkIcon = 'icon-checkmark-card.svg'
  // const exclaimIcon = 'icon-alert-card.svg'
  // const reviewIcon = 'icon-review-card.svg'

  return (
    <div className="usa-section">
      <div className="grid-container">
        <Image
          src="/images/states/co/icon-review-card.svg" // TODO dynamically load icons based on status
          alt=""
          width={100}
          height={75}
          aria-hidden="true"
        />
        <h1 className="font-family-sans margin-top-1">{t('title')}</h1>

        {['mixedEnrolled', 'noneEnrolled'].includes(householdEnrollmentResult) && (
          <section >
            <div className="usa-summary-box">
              <NotEnrolledSection
                results={notEnrolled}
                applicationUrl={applicationUrl}
              />
            </div>
            <div className="margin-top-3">
              <EnrolledSection results={enrolled} />
            </div>
          </section>
        )}

        {householdEnrollmentResult === 'allEnrolled' && (
          <div className="usa-summary-box" >
            <EnrolledSection results={enrolled} />
          </div>
        )}

        {householdEnrollmentResult === 'indeterminate' && (
          <section>
            <h2 className="font-family-sans">{t('errorTitle')}</h2>
            {errors.map((child) => (
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

        {['mixedEnrolled', 'indeterminate'].includes(householdEnrollmentResult) && (
          <section data-testid="next-steps">
            <h1 className="font-family-sans margin-top-1">Next Steps</h1>
            <ol className="usa-process-list">
              <li className="usa-process-list__item">{notEnrolledNextSteps}</li>
              <li className="usa-process-list__item">{enrolledNextSteps}</li>
            </ol>

            {eligibilityAccordion}
          </section>
        )}

        {householdEnrollmentResult === 'noneEnrolled' && (
          <section>
            {notEnrolledNextSteps}
            {eligibilityAccordion}
          </section>
        )}

        {householdEnrollmentResult === 'allEnrolled' && (
          <section>{enrolledNextSteps}</section>
        )}
      </div>
    </div>
  )
}
