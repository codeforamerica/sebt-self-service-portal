import { type IdOption } from '@/features/auth'

// DC-only: CO uses external auth and never reaches this route.
// What a user who answers "No" to "Do you receive SNAP or TANF?" chooses from.
export const DC_ID_OPTIONS: IdOption[] = [
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

// Asked for after "Yes". For co-loaded users the SNAP/TANF case number is the Household lookup
// key in DC's CMS.
export const DC_SNAP_TANF_OPTION: IdOption = {
  value: 'snapAccountId',
  labelKey: 'optionAccountId',
  inputLabelKey: 'labelAccountId',
  inputHelperKey: 'helperAccountId',
  // DC CSV: "typically 7 or 8 digits long".
  validation: { digits: [7, 8] }
}
