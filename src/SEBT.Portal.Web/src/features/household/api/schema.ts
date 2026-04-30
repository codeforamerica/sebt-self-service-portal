import { z } from 'zod'

// Backend enum values map to these strings
const APPLICATION_STATUS_MAP: Record<number, string> = {
  0: 'Unknown',
  1: 'Pending',
  2: 'Approved',
  3: 'Denied',
  4: 'UnderReview',
  5: 'Cancelled'
}

const CARD_STATUS_MAP: Record<number, string> = {
  0: 'Requested',
  1: 'Mailed',
  2: 'Active',
  3: 'Deactivated',
  4: 'Unknown',
  5: 'Processed',
  6: 'Lost',
  7: 'Stolen',
  8: 'Damaged',
  9: 'DeactivatedByState',
  10: 'NotActivated',
  11: 'Frozen',
  12: 'Undeliverable'
}

const ISSUANCE_TYPE_MAP: Record<number, string> = {
  0: 'Unknown',
  1: 'SummerEbt',
  2: 'TanfEbtCard',
  3: 'SnapEbtCard'
}

// Preprocess to convert integer enum values from backend to string enum values
// Unknown numeric values are mapped to 'Unknown' to handle future backend additions gracefully
export const IssuanceTypeSchema = z.preprocess(
  (val) =>
    typeof val === 'number'
      ? (ISSUANCE_TYPE_MAP[val as keyof typeof ISSUANCE_TYPE_MAP] ?? 'Unknown')
      : val,
  z.enum(['Unknown', 'SummerEbt', 'TanfEbtCard', 'SnapEbtCard'])
)

export type IssuanceType = z.infer<typeof IssuanceTypeSchema>

export const ApplicationStatusSchema = z.preprocess(
  (val) =>
    typeof val === 'number'
      ? (APPLICATION_STATUS_MAP[val as keyof typeof APPLICATION_STATUS_MAP] ?? 'Unknown')
      : val,
  z.enum(['Unknown', 'Pending', 'Approved', 'Denied', 'UnderReview', 'Cancelled'])
)

export type ApplicationStatus = z.infer<typeof ApplicationStatusSchema>

const CARD_STATUS_STRING_MAP: Record<string, string> = Object.fromEntries(
  Object.values(CARD_STATUS_MAP).map((v) => [v.toUpperCase(), v])
)

export const CardStatusSchema = z.preprocess(
  (val) =>
    typeof val === 'number'
      ? (CARD_STATUS_MAP[val as keyof typeof CARD_STATUS_MAP] ?? 'Unknown')
      : typeof val === 'string'
        ? (CARD_STATUS_STRING_MAP[val.toUpperCase()] ?? (val || 'Unknown'))
        : val,
  z.enum([
    'Unknown',
    'Requested',
    'Mailed',
    'Active',
    'Deactivated',
    'Processed',
    'Lost',
    'Stolen',
    'Damaged',
    'DeactivatedByState',
    'NotActivated',
    'Frozen',
    'Undeliverable'
  ])
)

export type CardStatus = z.infer<typeof CardStatusSchema>

/**
 * UI-facing card statuses displayed to the user.
 * Multiple backend statuses map to a single UI status
 * (e.g., Lost/Stolen/Damaged all show as "Inactive").
 */
export type UiCardStatus = 'Processed' | 'Active' | 'Inactive' | 'Frozen' | 'Undeliverable'

/**
 * Maps a backend CardStatus to the user-facing UI status.
 * The mapping follows the status table in the DC-130 ticket.
 */
export function toUiCardStatus(cardStatus: CardStatus): UiCardStatus {
  switch (cardStatus) {
    case 'Processed':
      return 'Processed'
    case 'Active':
      return 'Active'
    case 'Lost':
    case 'Stolen':
    case 'Damaged':
    case 'Deactivated':
    case 'DeactivatedByState':
    case 'NotActivated':
      return 'Inactive'
    case 'Frozen':
      return 'Frozen'
    case 'Undeliverable':
      return 'Undeliverable'
    case 'Requested':
    case 'Mailed':
    default:
      // Requested and Mailed are not in the status display spec (DC-95);
      // CardStatusDisplay returns null for these before this value is used.
      return 'Active'
  }
}

/**
 * Determines whether a card with this status is eligible for replacement.
 * Only cards reported as Lost, Stolen, or Damaged can be replaced.
 */
export function isReplacementEligible(cardStatus: CardStatus): boolean {
  return cardStatus === 'Lost' || cardStatus === 'Stolen' || cardStatus === 'Damaged'
}

export const ChildSchema = z.object({
  firstName: z.string(),
  lastName: z.string(),
  status: ApplicationStatusSchema.nullable().optional()
})

export type Child = z.infer<typeof ChildSchema>

export const AddressSchema = z.object({
  streetAddress1: z.string().nullable().optional(),
  streetAddress2: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional()
})

export type Address = z.infer<typeof AddressSchema>

export const SummerEbtCaseSchema = z.object({
  summerEBTCaseID: z.string().nullable().optional(),
  applicationId: z.string().nullable().optional(),
  applicationStudentId: z.string().nullable().optional(),
  childFirstName: z.string(),
  childLastName: z.string(),
  childDateOfBirth: z.string().nullable().optional(),
  householdType: z.string(),
  eligibilityType: z.string(),
  applicationDate: z.string().nullable().optional(),
  applicationStatus: ApplicationStatusSchema.nullable().optional(),
  mailingAddress: AddressSchema.nullable().optional(),
  ebtCaseNumber: z.string().nullable().optional(),
  caseDisplayNumber: z.string().nullable().optional(),
  ebtCardLastFour: z.string().nullable().optional(),
  ebtCardStatus: CardStatusSchema.nullable().optional(),
  ebtCardIssueDate: z.string().nullable().optional(),
  ebtCardBalance: z.number().nullable().optional(),
  benefitAvailableDate: z.string().nullable().optional(),
  benefitExpirationDate: z.string().nullable().optional(),
  eligibilitySource: z.string().nullable().optional(),
  issuanceType: IssuanceTypeSchema.nullable().optional(),
  // Card lifecycle timestamps — not yet populated by any state connector backend.
  // TODO: Add these fields to the state-connector SummerEbtCase interface model
  // so connectors can provide card fulfillment timeline data for enrolled children.
  cardRequestedAt: z.string().nullable().optional(),
  cardMailedAt: z.string().nullable().optional(),
  cardActivatedAt: z.string().nullable().optional(),
  cardDeactivatedAt: z.string().nullable().optional(),
  allowAddressChange: z.boolean().optional().default(true),
  allowCardReplacement: z.boolean().optional().default(true)
})

export type SummerEbtCase = z.infer<typeof SummerEbtCaseSchema>

export const ApplicationSchema = z.object({
  applicationNumber: z.string().nullable().optional(),
  caseNumber: z.string().nullable().optional(),
  applicationStatus: ApplicationStatusSchema,
  applicationDate: z.string().nullable().optional(),
  benefitIssueDate: z.string().nullable().optional(),
  benefitExpirationDate: z.string().nullable().optional(),
  last4DigitsOfCard: z.string().nullable().optional(),
  cardStatus: CardStatusSchema.nullable().optional(),
  cardRequestedAt: z.string().nullable().optional(),
  cardMailedAt: z.string().nullable().optional(),
  cardActivatedAt: z.string().nullable().optional(),
  cardDeactivatedAt: z.string().nullable().optional(),
  children: z.array(ChildSchema),
  childrenOnApplication: z.number(),
  issuanceType: IssuanceTypeSchema.nullable().optional()
})

export type Application = z.infer<typeof ApplicationSchema>

export const UserProfileSchema = z.object({
  firstName: z.string(),
  middleName: z.string().nullable().optional(),
  lastName: z.string().nullable().optional()
})

export type UserProfile = z.infer<typeof UserProfileSchema>

export const AllowedActionsSchema = z.object({
  canUpdateAddress: z.boolean(),
  canRequestReplacementCard: z.boolean(),
  addressUpdateDeniedMessageKey: z.string().nullable().optional(),
  cardReplacementDeniedMessageKey: z.string().nullable().optional()
})

export type AllowedActions = z.infer<typeof AllowedActionsSchema>

export const HouseholdDataSchema = z.object({
  // email is optional to support IAL authorization where user may not have access to PII
  email: z.string().nullable().optional(),
  phone: z.string().nullable().optional(),
  summerEbtCases: z.array(SummerEbtCaseSchema).optional().default([]),
  applications: z.array(ApplicationSchema),
  addressOnFile: AddressSchema.nullable().optional(),
  userProfile: UserProfileSchema.nullable().optional(),
  benefitIssuanceType: IssuanceTypeSchema.nullable().optional(),
  allowedActions: AllowedActionsSchema.nullable().optional()
})

export type HouseholdData = z.infer<typeof HouseholdDataSchema>

/**
 * Formats a US phone number as XXX-XXX-XXXX.
 * Strips non-digit characters and a leading country code (1) before formatting.
 * Returns the input unchanged if it does not resolve to exactly 10 digits.
 */
export function formatUsPhone(phone: string): string {
  let digits = phone.replace(/\D/g, '')
  if (digits.length === 11 && digits.startsWith('1')) {
    digits = digits.slice(1)
  }
  if (digits.length !== 10) return phone
  return `${digits.slice(0, 3)}-${digits.slice(3, 6)}-${digits.slice(6)}`
}

export function formatDate(isoDate: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, {
    month: '2-digit',
    day: '2-digit',
    year: 'numeric',
    timeZone: 'UTC'
  }).format(new Date(isoDate))
}

// Matches date placeholders in locale strings: [MM/DD/YYYY] (English) or [DD/MM/YYYY] (Spanish)
const DATE_PLACEHOLDER = /\[(?:MM\/DD\/YYYY|DD\/MM\/YYYY)\]/

// "Requested on [MM/DD/YYYY]" → "Requested on 01/15/2026" or "Requested"
// "Solicitada el [DD/MM/YYYY]" → "Solicitada el 15/01/2026" or "Solicitada"
export function interpolateDate(template: string, isoDate: string | null, locale: string): string {
  if (isoDate) {
    return template.replace(DATE_PLACEHOLDER, formatDate(isoDate, locale))
  }
  // Strip optional preceding connector word (" on", " el") along with the placeholder
  // eslint-disable-next-line security/detect-non-literal-regexp
  return template.replace(new RegExp(`(?:\\s+\\S+)?\\s*${DATE_PLACEHOLDER.source}`), '').trim()
}
