import { HelpSection } from '@/components/layout'
import { VerifyOtpFormWrapper } from '@/features/auth'
import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'
import { getTranslations } from '@/lib/translations'

export default function VerifyPage() {
  const state = getState()
  const links = getStateLinks(state)
  const t = getTranslations('login')
  const tCommon = getTranslations('common')

  return (
    <>
      <div className="usa-section">
        <div className="grid-container maxw-tablet">
          <section aria-labelledby="verify-title">
            <h1
              id="verify-title"
              className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
            >
              {t('verifyTitle')}
            </h1>

            <p className="margin-top-4 font-sans-sm">{tCommon('requiredFields')}</p>

            <VerifyOtpFormWrapper contactLink={links.external.contactUsAssistance} />
          </section>
        </div>
      </div>

      <HelpSection state={state} />
    </>
  )
}
