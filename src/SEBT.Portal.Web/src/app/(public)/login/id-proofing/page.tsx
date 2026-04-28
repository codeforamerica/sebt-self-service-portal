import { type IdOption } from '@/features/auth'
import { IdProofingWithDi } from '@/features/auth/components/id-proofing/IdProofingWithDi'
import { getTranslations } from '@/lib/translations'
import { getState, getStateLinks } from '@sebt/design-system'

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
    inputLabelKey: 'labelSsn',
    // SSN is federally 9 digits. Shared Zod schema also enforces this.
    validation: { digits: 9 }
  },
  {
    value: 'itin',
    labelKey: 'optionLabelItin',
    inputLabelKey: 'labelItin',
    // ITIN is federally 9 digits. Shared Zod schema also enforces this.
    validation: { digits: 9 }
  },
  {
    value: 'medicaidId',
    labelKey: 'optionLabelMedicaidId',
    helperKey: 'optionHelperMedicaidId',
    inputLabelKey: 'labelMedicaidId',
    // DC CSV: "typically 7 or 8 digits long".
    validation: { digits: [7, 8] }
  },
  {
    value: 'snapAccountId',
    labelKey: 'optionAccountId',
    helperKey: 'optionHelperAccountId',
    inputLabelKey: 'labelAccountId',
    // DC CSV: "typically 7 or 8 digits long".
    validation: { digits: [7, 8] }
  },
  {
    value: 'snapPersonId',
    labelKey: 'optionPersonId',
    helperKey: 'optionHelperPersonId',
    inputLabelKey: 'labelPersonId',
    // DC CSV: "typically 7 or 8 digits long".
    validation: { digits: [7, 8] }
  },
  {
    value: 'none',
    // Cross-namespace lookup: the label key "noneOfTheAbove" lives in the
    // common namespace (sourced from CSV row "GLOBAL - Option - None of the
    // above"). The form's useTranslation() targets the idProofing namespace,
    // so the "common:" prefix tells i18next to resolve from common instead.
    labelKey: 'common:noneOfTheAbove'
    // No validation: "none of the above" skips the ID value input entirely.
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
            contactLink={links.external.contactUsAssistance}
          />
        </section>
      </div>
    </div>
  )
}
