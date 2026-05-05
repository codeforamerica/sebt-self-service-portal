'use client'

import { IdProofingWithDi } from '@/features/auth/components/id-proofing/IdProofingWithDi'
import { getState, getStateLinks } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

import { DC_ID_OPTIONS, DC_ID_OPTIONS_CO_LOADED } from './dc-id-options'

export default function IdProofingPage() {
  const state = getState()
  const links = getStateLinks(state)
  const { t } = useTranslation('idProofing')
  const { t: tCommon } = useTranslation('common')

  return (
    <div className="usa-section">
      <div className="grid-container maxw-tablet">
        <section aria-labelledby="id-proofing-title">
          <h1
            id="id-proofing-title"
            className="font-sans-xl text-bold line-height-sans-1 margin-bottom-3"
          >
            {t('title')}
          </h1>

          <p className="margin-top-0 font-sans-sm">{t('body')}</p>

          <p className="margin-top-2 font-sans-sm">{tCommon('requiredFields')}</p>

          <IdProofingWithDi
            idOptions={DC_ID_OPTIONS}
            coLoadedIdOptions={DC_ID_OPTIONS_CO_LOADED}
            contactLink={links.external.contactUsAssistance}
          />
        </section>
      </div>
    </div>
  )
}
