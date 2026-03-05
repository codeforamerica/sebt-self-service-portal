import { IdProofingForm, type IdOption } from '@/features/auth'
import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'
import { getTranslations } from '@/lib/translations'

// DC-only: CO uses external auth and never reaches this route.
// If a future state adopts OTP auth with id-proofing, add a state-based options
// map or guard here.
//
// The full set shown here is for non-co-loaded users. Once the backend confirms
// which options are available for co-loaded users (via JWT claim), this list can
// be made dynamic at the page level.
// TODO: Filter options based on co-loaded status once that claim is available in the JWT.
const DC_ID_OPTIONS: IdOption[] = [
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
    value: 'medicaidId',
    labelKey: 'optionLabelMedicaidId',
    helperKey: 'optionHelperMedicaidId',
    inputLabelKey: 'labelMedicaidId'
  },
  {
    value: 'snapAccountId',
    labelKey: 'optionAccountId',
    helperKey: 'optionHelperAccountId',
    inputLabelKey: 'labelAccountId'
  },
  {
    value: 'snapPersonId',
    labelKey: 'optionPersonId',
    helperKey: 'optionHelperPersonId',
    inputLabelKey: 'labelPersonId'
  },
  {
    value: 'none',
    // TODO: Use t('optionLabelNone') once key is available in dc.csv
    labelKey: 'optionLabelNone'
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

          <IdProofingForm
            idOptions={DC_ID_OPTIONS}
            contactLink={links.external.contactUsAssistance}
          />
        </section>
      </div>
    </div>
  )
}
