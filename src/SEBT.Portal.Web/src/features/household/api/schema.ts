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
  0: 'Unknown',
  1: 'Requested',
  2: 'Mailed',
  3: 'Active',
  4: 'Deactivated'
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
  (val) => (typeof val === 'number' ? (ISSUANCE_TYPE_MAP[val] ?? 'Unknown') : val),
  z.enum(['Unknown', 'SummerEbt', 'TanfEbtCard', 'SnapEbtCard'])
)

export type IssuanceType = z.infer<typeof IssuanceTypeSchema>

export const ApplicationStatusSchema = z.preprocess(
  (val) => (typeof val === 'number' ? (APPLICATION_STATUS_MAP[val] ?? 'Unknown') : val),
  z.enum(['Unknown', 'Pending', 'Approved', 'Denied', 'UnderReview', 'Cancelled'])
)

export type ApplicationStatus = z.infer<typeof ApplicationStatusSchema>

export const CardStatusSchema = z.preprocess(
  (val) => (typeof val === 'number' ? (CARD_STATUS_MAP[val] ?? 'Unknown') : val),
  z.enum(['Unknown', 'Requested', 'Mailed', 'Active', 'Deactivated'])
)

export type CardStatus = z.infer<typeof CardStatusSchema>

export const ChildSchema = z.object({
  caseNumber: z.number().nullable().optional(),
  firstName: z.string(),
  lastName: z.string()
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

export const ApplicationSchema = z.object({
  applicationNumber: z.string().nullable().optional(),
  caseNumber: z.string().nullable().optional(),
  applicationStatus: ApplicationStatusSchema,
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

export const HouseholdDataSchema = z.object({
  // email is optional to support IAL authorization where user may not have access to PII
  email: z.string().nullable().optional(),
  phone: z.string().nullable().optional(),
  applications: z.array(ApplicationSchema),
  addressOnFile: AddressSchema.nullable().optional(),
  userProfile: UserProfileSchema.nullable().optional(),
  benefitIssuanceType: IssuanceTypeSchema.nullable().optional()
})

export type HouseholdData = z.infer<typeof HouseholdDataSchema>
