import { type IdOption } from '@/features/auth'
import { IdProofingWithDi } from '@/features/auth/components/id-proofing/IdProofingWithDi'
import { getTranslations } from '@/lib/translations'
import { getState, getStateLinks } from '@sebt/design-system'

// DC-only: CO uses external auth and never reaches this route.
const DC_ID_OPTIONS: IdOption[] = [
  {
    value: 'snapAccountId',
    labelKey: 'optionAccountId',
    helperKey: 'optionHelperAccountId',
    inputLabelKey: 'labelAccountId'
  },
  {
    value: 'ssn',
    labelKey: 'optionLabelSsn',
    inputLabelKey: 'labelSsn'
  },
  {
    value: 'itin',
    labelKey: 'optionLabelItin',
    inputLabelKey: 'labelItin'
  },
  {
    value: 'none',
    labelKey: 'common:noneOfTheAbove',
    dividerBefore: true
  }
]

// For co-loaded users, the SNAP/TANF account ID is the Household lookup key in DC's CMS.
const DC_ID_OPTIONS_CO_LOADED: IdOption[] = [
  {
    value: 'snapAccountId',
    labelKey: 'optionAccountId',
    helperKey: 'optionHelperAccountId',
    inputLabelKey: 'labelAccountId'
  },
  {
    value: 'itin',
    labelKey: 'optionLabelItin',
    inputLabelKey: 'labelItin'
  },
  {
    value: 'none',
    labelKey: 'common:noneOfTheAbove',
    dividerBefore: true
  }
]

export default function IdProofingPage() {
  const state = getState()
  const links = getStateLinks(state)
  const t = getTranslations('idProofing')
  const tCommon = getTranslations('common')

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
