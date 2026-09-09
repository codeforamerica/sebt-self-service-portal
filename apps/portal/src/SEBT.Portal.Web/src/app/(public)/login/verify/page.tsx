'use client'

import { VerifyOtpFormWrapper } from '@/features/auth'
import { getState, getStateLinks } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

export default function VerifyPage() {
  const state = getState()
  const links = getStateLinks(state)
  const { t } = useTranslation('login')
  const { t: tCommon } = useTranslation('common')

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
    </>
  )
}
