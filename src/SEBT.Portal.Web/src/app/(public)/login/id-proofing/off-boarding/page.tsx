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

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="off-boarding-title">
          <OffBoardingContent
            title={t('title')}
            body={t('body1')}
            backHref="/login/id-proofing"
            contactHref={links.help.contactUs}
            contactLabel={t('action1')}
            canApply={canApply}
            applyBody={t('body2') || undefined}
            applySkipBody={t('body3') || undefined}
            applyLabel={t('action2') || undefined}
            // TODO: Add state-specific apply URL to StateLinks once the application flow is finalized
            applyHref="#"
          />
        </section>
      </div>
    </div>
  )
}
