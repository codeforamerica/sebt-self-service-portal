'use client'

import { useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { OffBoardingContent, useAuth } from '@/features/auth'
import { getApplyHref } from '@/lib/applyHref'
import { getState, getStateLinks } from '@sebt/design-system'

export default function OffBoardingPage() {
  const searchParams = useSearchParams()
  const reason = searchParams.get('reason')
  const canApplyParam = searchParams.get('canApply') !== 'false'

  const { session } = useAuth()
  const isCoLoaded = session?.isCoLoaded === true

  const { t, i18n } = useTranslation('offBoarding')
  const { t: tCommon } = useTranslation('common')

  const state = getState()
  const links = getStateLinks(state)

  // Prefer the web contact page; fall back to help desk email for states
  // where the contact URL is not yet available (e.g., CO uses a mailto link).
  const contactHref =
    links.help.contactUs !== '#' ? links.help.contactUs : (links.help.helpDeskEmail ?? '#')

  // Branch order: co-loaded copy wins, then reason-specific copy for the
  // non-co-loaded path, then generic offBoarding copy.
  // - Co-loaded users cannot off-board to Socure DocV per PRD; they see a
  //   "cannot identify you" screen instead of the DocV-flavored copy.
  // - Reason-specific branches force canApply=false until product decides
  //   which failure modes allow re-application.
  // TODO: Replace hardcoded strings with t(...) keys once they exist in dc.csv.
  let title: string
  let body: string
  let canApply = canApplyParam
  let contactLabel: string
  let applyBody: string | undefined
  let applySkipBody: string | undefined
  let applyLabel: string | undefined

  if (isCoLoaded) {
    title = t('coLoadedTitle')
    body = t('coLoadedBody1')
    contactLabel = t('coLoadedAction1')
    applyBody = t('coLoadedBody2', '') || undefined
    applySkipBody = undefined
    applyLabel = t('coLoadedAction2', '') || undefined
  } else if (reason === 'noIdProvided') {
    title = 'We need an ID to verify you'
    body =
      "To confirm your identity, we need one of the listed IDs. If you don't have any of these IDs, contact us for help."
    canApply = false
    contactLabel = tCommon('linkContactUs')
    applyBody = undefined
    applySkipBody = undefined
    applyLabel = undefined
  } else if (reason === 'docVerificationFailed') {
    title = "We couldn't verify your identity"
    body =
      "Your document couldn't be verified. You can try again with a different ID, or contact us if you need help."
    canApply = false
    contactLabel = tCommon('linkContactUs')
    applyBody = undefined
    applySkipBody = undefined
    applyLabel = undefined
  } else {
    title = t('title')
    body = t('body1')
    // TODO: Use t('action1') once key is available in dc.csv
    contactLabel = tCommon('linkContactUs')
    applyBody = t('body2', '') || undefined
    applySkipBody = t('body3', '') || undefined
    applyLabel = t('action2', '') || undefined
  }

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="off-boarding-title">
          <OffBoardingContent
            title={title}
            body={body}
            backHref="/login/id-proofing"
            backLabel={t('action', '') || tCommon('back')}
            contactHref={contactHref}
            contactLabel={contactLabel}
            canApply={canApply}
            applyBody={applyBody}
            applySkipBody={applySkipBody}
            applyLabel={applyLabel}
            applyHref={getApplyHref(i18n.language)}
          />
        </section>
      </div>
    </div>
  )
}
