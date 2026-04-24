'use client'

import { useSearchParams } from 'next/navigation'
import { useTranslation } from 'react-i18next'

import { OffBoardingContent, useAuth } from '@/features/auth'
import { getState, getStateLinks } from '@sebt/design-system'

export default function OffBoardingPage() {
  const searchParams = useSearchParams()
  const canApply = searchParams.get('canApply') !== 'false'

  const { session } = useAuth()
  const isCoLoaded = session?.isCoLoaded === true

  const { t } = useTranslation('offBoarding')
  const { t: tCommon } = useTranslation('common')

  const state = getState()
  const links = getStateLinks(state)

  // Prefer the web contact page; fall back to help desk email for states
  // where the contact URL is not yet available (e.g., CO uses a mailto link).
  const contactHref =
    links.help.contactUs !== '#' ? links.help.contactUs : (links.help.helpDeskEmail ?? '#')

  // Co-loaded users cannot off-board to Socure DocV per PRD — they see a
  // "cannot identify you" screen instead of the DocV-flavored copy.
  const content = isCoLoaded
    ? {
        title: t('coLoadedTitle'),
        body: t('coLoadedBody1'),
        contactLabel: t('coLoadedAction1'),
        applyBody: t('coLoadedBody2', '') || undefined,
        applySkipBody: undefined,
        applyLabel: t('coLoadedAction2', '') || undefined
      }
    : {
        title: t('title'),
        body: t('body1'),
        // TODO: Use t('action1') once key is available in dc.csv
        contactLabel: tCommon('linkContactUs'),
        applyBody: t('body2', '') || undefined,
        applySkipBody: t('body3', '') || undefined,
        applyLabel: t('action2', '') || undefined
      }

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="off-boarding-title">
          <OffBoardingContent
            title={content.title}
            body={content.body}
            backHref="/login/id-proofing"
            backLabel={t('action', '') || tCommon('back')}
            contactHref={contactHref}
            contactLabel={content.contactLabel}
            canApply={canApply}
            applyBody={content.applyBody}
            applySkipBody={content.applySkipBody}
            applyLabel={content.applyLabel}
            applyHref="/apply"
          />
        </section>
      </div>
    </div>
  )
}
