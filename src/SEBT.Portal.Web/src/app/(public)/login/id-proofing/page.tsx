'use client'

import { type IdOption } from '@/features/auth'
import { IdProofingWithDi } from '@/features/auth/components/id-proofing/IdProofingWithDi'
import { getState, getStateLinks } from '@sebt/design-system'
import { useTranslation } from 'react-i18next'

// DC-only: CO uses external auth and never reaches this route.
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
    labelKey: 'common:noneOfTheAbove',
    // No validation: "none of the above" skips the ID value input entirely.
    dividerBefore: true
  }
]

// For co-loaded users, the SNAP/TANF account ID is the Household lookup key in DC's CMS.
const DC_ID_OPTIONS_CO_LOADED: IdOption[] = [
  {
    value: 'snapAccountId',
    labelKey: 'optionAccountId',
    helperKey: 'optionHelperAccountId',
    inputLabelKey: 'labelAccountId',
    // DC CSV: "typically 7 or 8 digits long".
    validation: { digits: [7, 8] }
  },
  {
    value: 'itin',
    labelKey: 'optionLabelItin',
    inputLabelKey: 'labelItin',
    // ITIN is federally 9 digits. Shared Zod schema also enforces this.
    validation: { digits: 9 }
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
