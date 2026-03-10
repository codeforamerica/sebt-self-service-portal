import { OffBoardingContent } from '@/features/auth'
import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'
import { getTranslations } from '@/lib/translations'

interface OffBoardingPageProps {
  searchParams: Promise<{ canApply?: string }>
}

export default async function OffBoardingPage({ searchParams }: OffBoardingPageProps) {
  const params = await searchParams
  const canApply = params.canApply !== 'false'

  const state = getState()
  const links = getStateLinks(state)
  const t = getTranslations('offBoarding')

  // Prefer the web contact page; fall back to help desk email for states
  // where the contact URL is not yet available (e.g., CO uses a mailto link).
  const contactHref =
    links.help.contactUs !== '#' ? links.help.contactUs : (links.help.helpDeskEmail ?? '#')

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="off-boarding-title">
          <OffBoardingContent
            title={t('title')}
            body={t('body1')}
            backHref="/login/id-proofing"
            contactHref={contactHref}
            contactLabel={t('action1')}
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
