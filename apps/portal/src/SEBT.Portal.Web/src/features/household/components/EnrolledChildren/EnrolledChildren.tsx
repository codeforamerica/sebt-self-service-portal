'use client'

import { useTranslation } from 'react-i18next'

import { useApplyHref } from '@/lib/useApplyHref'

import { useRequiredHouseholdData } from '../../api'
import { useHouseholdCardDetailsLoading } from '../../context/HouseholdCardDetailsLoadingContext'
import { ChildCard } from '../ChildCard'

// Keys map to CSV: "S2 - Portal Dashboard - Section Enrolled Children - {Key}"
export function EnrolledChildren() {
  const { t } = useTranslation('dashboard')
  const data = useRequiredHouseholdData()
  const cardDetailsLoading = useHouseholdCardDetailsLoading()
  const applyHref = useApplyHref()

  return (
    <section aria-labelledby="enrolled-children-heading">
      <h2
        id="enrolled-children-heading"
        className="font-heading-lg margin-bottom-1"
      >
        {t('sectionEnrolledChildrenHeading')}
      </h2>
      {/* The sheet authors a body row per season. The open-season copy trails off
          into the apply link ("…you may"), so a closed season cannot reuse it
          minus the link — it needs its own complete sentence. States without a
          separate closed season author both rows identically.

          The apply link renders only when applications are open AND the sheet has
          the link copy authored — CO leaves the action row empty, and t() with an
          empty-string default distinguishes a missing key from a raw-key fallback. */}
      <p className="margin-bottom-3">
        {applyHref ? t('sectionEnrolledChildrenBody1') : t('closedSectionEnrolledChildrenBody1')}
        {applyHref && t('sectionEnrolledChildrenAction', '') && (
          <>
            {' '}
            <a
              href={applyHref}
              className="usa-link"
            >
              {t('sectionEnrolledChildrenAction')}
            </a>
          </>
        )}
      </p>

      <div
        className="usa-accordion usa-accordion--bordered"
        data-allow-multiple
      >
        {data.summerEbtCases.map((c, index) => (
          <ChildCard
            key={`${c.childFirstName}-${c.childLastName}-${c.childDateOfBirth}-${c.summerEBTCaseID}`}
            summerEbtCase={c}
            defaultExpanded={index === 0}
            canRequestReplacementCard={data.allowedActions?.canRequestReplacementCard}
            cardDetailsLoading={cardDetailsLoading}
          />
        ))}
      </div>
    </section>
  )
}
