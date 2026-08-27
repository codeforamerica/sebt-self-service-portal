'use client'

import { getApplyHref } from '@/lib/applyHref'
import { getCheckerAssetPath } from '@/lib/checkerAssetPath'
import { allowsSequentialChecks, getFlowConfig } from '@/lib/flowConfig'
import Image from 'next/image'
import { useTranslation } from 'react-i18next'
import type { ChildCheckApiResponse } from '../schemas/enrollmentSchema'

import { RichText } from '@sebt/design-system'
import { mapApiStatus } from '../schemas/enrollmentSchema'
import { CheckAnotherChildCard } from './CheckAnotherChildCard'
import { ChildResultCard } from './ChildResultCard'
import { EnrolledSection } from './EnrolledSection'
import { NotEnrolledSection } from './NotEnrolledSection'
import { getState, getStateConfig } from '@sebt/design-system/src/lib/state'

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
  const { pageTitleText } = getStateConfig(getState())

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

  const { distinguishNoResults, resultsLayout } = getFlowConfig()
  const summarizesHousehold = resultsLayout === 'household'

  const enrolled = results.filter((r) => mapApiStatus(r.status) === 'enrolled')
  const matchFailures = results.filter((r) => mapApiStatus(r.status) === 'notEnrolled')
  const unresolved = results.filter((r) => mapApiStatus(r.status) === 'error')

  // Where a state's backend cannot tell "no match at all" from "found but not
  // matched", both arrive here as the same real-world answer, so they get the
  // same treatment rather than a separate screen for an unreachable state.
  const notEnrolled = distinguishNoResults ? matchFailures : [...matchFailures, ...unresolved]
  const errors = distinguishNoResults ? unresolved : []

  const householdEnrollmentResult = computeHouseholdEnrollmentResult(
    enrolled.length,
    notEnrolled.length
  )

  // A household summary covers several outcomes at once, so it keeps one
  // neutral heading and names each child below it. A single-outcome flow has
  // one answer to give, and that answer is the heading.
  const titleKey = summarizesHousehold
    ? 'title'
    : enrolled.length > 0
      ? 'streamlinedEnrolledTitle'
      : 'applyForSebtTitle'

  // The outcome-to-artwork mapping is state-specific, so it lives in the asset
  // manifest rather than in filenames chosen here.
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
        <h1 className={`font-family-sans font-sans-xl ${pageTitleText} margin-top-1`}>
          {t(titleKey)}
        </h1>

        {/* Only a household summary names the children it checked. */}
        {summarizesHousehold &&
          ['mixedEnrolled', 'allEnrolled'].includes(householdEnrollmentResult) && (
            <div className="usa-summary-box">
              <EnrolledSection results={enrolled} />
            </div>
          )}

        {summarizesHousehold && householdEnrollmentResult === 'mixedEnrolled' && (
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

        {summarizesHousehold && householdEnrollmentResult === 'noneEnrolled' && (
          <div className="usa-summary-box">
            <NotEnrolledSection results={notEnrolled} />
          </div>
        )}

        {summarizesHousehold && householdEnrollmentResult === 'indeterminate' && (
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

        {summarizesHousehold && householdEnrollmentResult === 'mixedEnrolled' && applyHref && (
          <section data-testid="next-steps">
            <h2 className="font-family-sans margin-top-4">
              {t('streamlinedEnrolledStepsHeading')}
            </h2>
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
        {summarizesHousehold && householdEnrollmentResult === 'mixedEnrolled' && !applyHref && (
          <>
            <section>{portalNextStep}</section>
            <section className="margin-top-3">
              <p>{t('applyForSebtBody2')}</p>
              {apply2027Block('apply2027Note')}
            </section>
          </>
        )}

        {summarizesHousehold && householdEnrollmentResult === 'noneEnrolled' && (
          <section className="margin-top-3">{apply2027Block('apply2027Note')}</section>
        )}

        {summarizesHousehold && householdEnrollmentResult === 'indeterminate' && (
          <section className="margin-top-3">
            <p>{t('applyForSebtBody2')}</p>
            {apply2027Block('apply2027Note')}
          </section>
        )}

        {summarizesHousehold && householdEnrollmentResult === 'allEnrolled' && (
          <section>{portalNextStep}</section>
        )}

        {/* One answer for one child: the heading states the outcome and the
            body explains what it means. An enrolled student then gets the
            portal, which is where the benefit details live. */}
        {!summarizesHousehold && (
          <section className="margin-top-3">
            <div className="usa-prose">
              <RichText>
                {t(enrolled.length > 0 ? 'streamlinedEnrolledBody' : 'applyForSebtBody')}
              </RichText>
            </div>

            {enrolled.length > 0 && (
              <section data-testid="next-step-portal">
                <h2 className="font-family-sans font-sans-md margin-top-4">
                  {t('streamlinedEnrolledAlertTitle')}
                </h2>
                <div className="usa-prose margin-top-2">
                  <RichText>{t('streamlinedEnrolledAlertBody')}</RichText>
                  <p>{t('streamlinedEnrolledAlertAction')}</p>
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
            )}
          </section>
        )}

        {/* Single-child flows finish one child at a time, so the results are
            where the next check begins. */}
        {allowsSequentialChecks() && (
          <CheckAnotherChildCard
            copy={enrolled.length > 0 ? 'streamlinedEnrolledCard2' : 'applyForSebtCard2'}
          />
        )}
      </div>
    </div>
  )
}
