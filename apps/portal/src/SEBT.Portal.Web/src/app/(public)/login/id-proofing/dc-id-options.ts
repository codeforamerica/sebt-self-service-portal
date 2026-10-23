import { type IdOption } from '@/features/auth'

// DC-only: CO uses external auth and never reaches this route.
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
    value: 'snapAccountId',
    labelKey: 'optionAccountId',
    helperKey: 'optionHelperAccountId',
    inputLabelKey: 'labelAccountId',
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
export const DC_ID_OPTIONS_CO_LOADED: IdOption[] = [
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

// With enable_socure_snap_tanf_question on, the form asks "Do you receive SNAP or TANF?" first.
// "Yes" asks only for the case number below; "No" chooses from the remaining ID options.
export const DC_SNAP_TANF_OPTION: IdOption = {
  value: 'snapAccountId',
  labelKey: 'optionAccountId',
  inputLabelKey: 'labelAccountId',
  inputHelperKey: 'helperAccountId',
  // DC CSV: "typically 7 or 8 digits long".
  validation: { digits: [7, 8] }
}

export const DC_ID_OPTIONS_AFTER_NO = DC_ID_OPTIONS.filter(
  (option) => option.value !== 'snapAccountId'
)
