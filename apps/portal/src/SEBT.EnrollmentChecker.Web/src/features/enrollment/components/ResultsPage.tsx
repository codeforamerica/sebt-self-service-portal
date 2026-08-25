'use client'

import { getApplyHref } from '@/lib/applyHref'
import { getCheckerAssetPath } from '@/lib/checkerAssetPath'
import Image from 'next/image'
import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'

import { RichText } from '@sebt/design-system'
import { mapApiStatus } from '../schemas/enrollmentSchema'
import { ChildResultCard } from './ChildResultCard'
import { EnrolledSection } from './EnrolledSection'
import { NotEnrolledSection } from './NotEnrolledSection'

interface ResultsPageProps {
  results: ChildCheckApiResponse[]
  portalUrl: string
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

export function ResultsPage({ results, portalUrl }: ResultsPageProps) {
  const { t, i18n } = useTranslation('result')

  // Null when no application destination is configured (applications closed,
  // DC-701): the 2027 application link and its wait note degrade away while the
  // closure line stays. The mixed variant also drops its numbered next-steps
  // list, since the portal step is then the only actionable one.
  const applyHref = getApplyHref(i18n.language)

  // Closure copy plus the optional summer-2027 application link. The wait-note
  // key differs by surface (standalone vs numbered step), so callers pass it.
  const apply2027Block = (noteKey: 'apply2027Note' | 'apply2027StepNote') => (
    <>
      <p>{t('enrollmentClosedBody')}</p>
      {applyHref && (
        <>
          <p>
            <a
              href={applyHref}
              data-analytics-cta="apply_cta"
              data-testid="apply-2027-link"
            >
              {t('apply2027Action')}
            </a>
          </p>
          <RichText>{t(noteKey)}</RichText>
        </>
      )}
    </>
  )

  const portalNextStep = (
    <section data-testid="next-step-portal">
      <h2 className="usa-process-list__heading margin-top-4">
        {t('streamlinedEnrolledAlertTitle')}
      </h2>
      <div className="margin-top-2">
        <RichText>{t('streamlinedEnrolledAlertBody')}</RichText>
      </div>
      <p>
        <a
          href={portalUrl}
          className="usa-button"
          data-testid="portal-link"
        >
          {t('streamlinedEnrolledAction')}
        </a>
      </p>
    </section>
  )

  const apply2027NextStep = (
    <section data-testid="next-step-apply-2027">
      <h2 className="usa-process-list__heading margin-top-4">{t('apply2027StepTitle')}</h2>
      <p className="margin-top-05">{t('applyForSebtBody2')}</p>
      {apply2027Block('apply2027StepNote')}
    </section>
  )

  const enrolled = results.filter((r) => mapApiStatus(r.status) === 'enrolled')
  const notEnrolled = results.filter((r) => mapApiStatus(r.status) === 'notEnrolled')
  const errors = results.filter((r) => mapApiStatus(r.status) === 'error')

  const householdEnrollmentResult = computeHouseholdEnrollmentResult(
    enrolled.length,
    notEnrolled.length
  )

  // Which artwork each outcome leads with is state-specific — CO opens an
  // enrolled result with the review card, DC with a checkmark — so the mapping
  // lives in the asset manifest rather than in filenames chosen here.
  const icon = getCheckerAssetPath(
    ['noneEnrolled', 'indeterminate'].includes(householdEnrollmentResult)
      ? 'resultsNotEnrolled'
      : 'resultsEnrolled'
  )

  return (
    <div className="usa-section">
      <div className="grid-container">
        {icon && (
          <Image
            src={icon}
            alt=""
            width={100}
            height={75}
            aria-hidden="true"
          />
        )}
        <h1 className="font-family-sans text-primary margin-top-1">{t('title')}</h1>

        {['mixedEnrolled', 'allEnrolled'].includes(householdEnrollmentResult) && (
          <div className="usa-summary-box">
            <EnrolledSection results={enrolled} />
          </div>
        )}

        {householdEnrollmentResult === 'mixedEnrolled' && (
          <section
            className="margin-top-3"
            data-testid="not-enrolled-inline"
          >
            <RichText>{t('notEnrolledInlineTitle')}</RichText>
            <ul>
              {notEnrolled.map((child) => (
                <ChildResultCard
                  key={child.checkId}
                  firstName={child.firstName}
                  lastName={child.lastName}
                  displayStatus="notEnrolled"
                />
              ))}
            </ul>
          </section>
        )}

        {householdEnrollmentResult === 'noneEnrolled' && (
          <div className="usa-summary-box">
            <NotEnrolledSection results={notEnrolled} />
          </div>
        )}

        {householdEnrollmentResult === 'indeterminate' && (
          <div
            className="usa-summary-box"
            data-testid="no-info-summary-box"
          >
            <section>
              <h4 className="usa-summary-box__heading">{t('noneBody1')}</h4>
              <div className="usa-summary-box__text">
                <ul>
                  {errors.map((child) => (
                    <ChildResultCard
                      key={child.checkId}
                      firstName={child.firstName}
                      lastName={child.lastName}
                      displayStatus="error"
                    />
                  ))}
                </ul>
              </div>
            </section>
          </div>
        )}

        {householdEnrollmentResult === 'mixedEnrolled' && applyHref && (
          <section data-testid="next-steps">
            <h1 className="font-family-sans margin-top-4">
              {t('streamlinedEnrolledStepsHeading')}
            </h1>
            <ol className="usa-process-list  margin-top-1">
              <li className="usa-process-list__item margin-top-2">{portalNextStep}</li>
              <li className="usa-process-list__item margin-top-2">{apply2027NextStep}</li>
            </ol>
          </section>
        )}

        {/* Without an application destination the 2027 step would be an
            instruction with nothing to act on, so the portal step stands alone
            (as on the all-enrolled page) and the not-enrolled children get the
            same explanation and closure line as the no-results page. */}
        {householdEnrollmentResult === 'mixedEnrolled' && !applyHref && (
          <>
            <section>{portalNextStep}</section>
            <section className="margin-top-3">
              <p>{t('applyForSebtBody2')}</p>
              {apply2027Block('apply2027Note')}
            </section>
          </>
        )}

        {householdEnrollmentResult === 'noneEnrolled' && (
          <section className="margin-top-3">{apply2027Block('apply2027Note')}</section>
        )}

        {householdEnrollmentResult === 'indeterminate' && (
          <section className="margin-top-3">
            <p>{t('applyForSebtBody2')}</p>
            {apply2027Block('apply2027Note')}
          </section>
        )}

        {householdEnrollmentResult === 'allEnrolled' && <section>{portalNextStep}</section>}
      </div>
    </div>
  )
}
