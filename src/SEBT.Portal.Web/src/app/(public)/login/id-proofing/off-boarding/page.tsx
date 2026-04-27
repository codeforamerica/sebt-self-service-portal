import { OffBoardingContent } from '@/features/auth'
import { getTranslations } from '@/lib/translations'
import { getState, getStateLinks } from '@sebt/design-system'

interface OffBoardingPageProps {
  searchParams: Promise<{ canApply?: string; reason?: string }>
}

export default async function OffBoardingPage({ searchParams }: OffBoardingPageProps) {
  const params = await searchParams
  const canApplyParam = params.canApply !== 'false'

  const state = getState()
  const links = getStateLinks(state)
  const t = getTranslations('offBoarding')

  // Prefer the web contact page; fall back to help desk email for states
  // where the contact URL is not yet available (e.g., CO uses a mailto link).
  const contactHref =
    links.help.contactUs !== '#' ? links.help.contactUs : (links.help.helpDeskEmail ?? '#')

  // Reason-specific copy. Each branch overrides the generic offBoarding.json text
  // (which reads like a DocV lead-in) with tone-appropriate messaging, and forces
  // canApply=false to suppress the Apply-now block until product decides which
  // failure modes allow re-application.
  // TODO: Replace hardcoded strings with t(...) keys once they exist in dc.csv.
  let title = t('title')
  let body = t('body1')
  let canApply = canApplyParam

  if (params.reason === 'noIdProvided') {
    title = 'We need an ID to verify you'
    body =
      "To confirm your identity, we need one of the listed IDs. If you don't have any of these IDs, contact us for help."
    canApply = false
  } else if (params.reason === 'docVerificationFailed') {
    title = "We couldn't verify your identity"
    body =
      "Your document couldn't be verified. You can try again with a different ID, or contact us if you need help."
    canApply = false
  }

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="off-boarding-title">
          <OffBoardingContent
            title={title}
            body={body}
            backHref="/login/id-proofing"
            contactHref={contactHref}
            // TODO: Use t('action1') once key is available in dc.csv
            contactLabel="Contact us"
            canApply={canApply}
            applyBody={t('body2', '') || undefined}
            applySkipBody={t('body3', '') || undefined}
            applyLabel={t('action2', '') || undefined}
            // TODO: Pass applyHref once the state-specific apply URL is added to StateLinks
          />
        </section>
      </div>
    </div>
  )
}
