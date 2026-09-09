'use client'

import { useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { OffBoardingContent, useAuth } from '@/features/auth'
import { useApplyHref } from '@/lib/useApplyHref'
import { getState, getStateLinks } from '@sebt/design-system'

export default function OffBoardingPage() {
  const searchParams = useSearchParams()
  const reason = searchParams.get('reason')
  const canApplyParam = searchParams.get('canApply') !== 'false'

  const { session } = useAuth()
  const isCoLoaded = session?.isCoLoaded === true
  const useCoLoadedOffboarding = isCoLoaded || reason === 'coLoadedOnly'

  const { t } = useTranslation('offBoarding')
  const { t: tDashboard } = useTranslation('dashboard')
  const { t: tCommon } = useTranslation('common')
  const { t: tStepUpFailure } = useTranslation('stepUpFailure')

  const state = getState()
  const links = getStateLinks(state)

  // Null when applications are closed (enable_apply flag off) or the state has
  // no apply destination (DC since DC-701); that suppresses the apply section
  // below regardless of the query param.
  const applyHref = useApplyHref()

  // Prefer the web contact page; fall back to help desk email for states
  // where the contact URL is not yet available (e.g., CO uses a mailto link).
  const contactHref =
    links.help.contactUs !== '#' ? links.help.contactUs : (links.help.helpDeskEmail ?? '#')

  // Branch order: OIDC `/callback` failures, then co-loaded copy (session flag or
  // coLoadedOnly reason from household cohort lookup during ID proofing), then
  // reason-specific copy for the non-co-loaded path, then generic offBoarding copy.
  let title: string
  let body: string
  let backHref = '/login/id-proofing'
  let canApply = canApplyParam && applyHref !== null
  let contactLabel: string
  let applyBody: string | undefined
  let applySkipBody: string | undefined
  let applyLabel: string | undefined
  let bodyList: string[] | undefined
  let bodyNote: string | undefined
  let continueHref: string | undefined
  let continueLabel: string | undefined

  if (reason === 'oidcCallbackError') {
    title =
      tStepUpFailure('title') || "We're sorry, we aren't able to show your Summer EBT information"
    body = tStepUpFailure('body') || 'You can contact us if you need more help.'
    backHref = '/dashboard'
    canApply = false
    contactLabel = tCommon('linkContactUs')
    applyBody = undefined
    applySkipBody = undefined
    applyLabel = undefined
  } else if (useCoLoadedOffboarding) {
    title = t('coLoadedTitle')
    body = t('coLoadedBody1')
    contactLabel = t('coLoadedAction1')
    applyBody = t('coLoadedBody2', '') || undefined
    applySkipBody = undefined
    applyLabel = t('coLoadedAction2', '') || undefined
  } else if (reason === 'noQualifyingHousehold') {
    title = tDashboard('alertApplicationsTitle')
    body = tDashboard('alertApplicationsBody')
    contactLabel = tCommon('linkContactUs')
    applyBody = undefined
    applySkipBody = undefined
    applyLabel = tDashboard('alertApplicationsAction')
  } else if (reason === 'noIdProvided') {
    // TODO REMOVE HARDCODED STRINGS
    title = 'We need an ID to verify you'
    body =
      "To confirm your identity, we need one of the listed IDs. If you don't have any of these IDs, contact us for help."
    canApply = false
    contactLabel = tCommon('linkContactUs')
    applyBody = undefined
    applySkipBody = undefined
    applyLabel = undefined
  } else if (reason === 'docVerificationEgregiousFailed') {
    // i18next returns '' (not the fallback arg) when a key exists with an empty value.
    title = t('docVerificationFailedTitle') || "We couldn't verify your identity"
    body =
      t('docVerificationFailedBody') ||
      "Your document couldn't be verified. You can try again with a different ID, or contact us if you need help."
    canApply = false
    contactLabel = tCommon('linkContactUs')
    applyBody = undefined
    applySkipBody = undefined
    applyLabel = undefined
  } else {
    // Generic "We want to keep your account safe" screen. Both inline Socure
    // rejects (reason=idProofingFailed) and webhook rejects/resubmits land here.
    // The accepted-ID list and skip note are core body content; the primary
    // action is "Continue" (forward), with "Enter an ID number" as the back
    // affordance. Both route to the form; contact lives in the global help band.
    title = t('title')
    body = t('body1')
    contactLabel = tCommon('linkContactUs')
    bodyList = (t('body2', '') || '').split('\n').filter(Boolean)
    bodyNote = t('body3', '') || undefined
    continueHref = '/login/id-proofing'
    continueLabel = tCommon('continue')
  }

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="off-boarding-title">
          <OffBoardingContent
            title={title}
            body={body}
            backHref={backHref}
            backLabel={t('action', '') || tCommon('back')}
            contactHref={contactHref}
            contactLabel={contactLabel}
            canApply={canApply}
            applyBody={applyBody}
            applySkipBody={applySkipBody}
            applyLabel={applyLabel}
            applyHref={applyHref ?? undefined}
            bodyList={bodyList}
            bodyNote={bodyNote}
            continueHref={continueHref}
            continueLabel={continueLabel}
          />
        </section>
      </div>
    </div>
  )
}
