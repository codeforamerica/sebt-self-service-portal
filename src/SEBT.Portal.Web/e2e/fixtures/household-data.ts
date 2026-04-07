/**
 * Factory functions for mock API responses used in card-replacement E2E tests.
 *
 * Integer enum values match what the real .NET backend returns.
 * The frontend Zod schema preprocesses them to string enums before use.
 *
 * IssuanceType: 0=Unknown, 1=SummerEbt, 2=TanfEbtCard, 3=SnapEbtCard
 * ApplicationStatus: 0=Unknown, 1=Pending, 2=Approved, 3=Denied, 4=UnderReview, 5=Cancelled
 * CardStatus: 0=Requested, 1=Mailed, 2=Active, 3=Deactivated
 */

import { currentState, type StateCode } from './state'

/**
 * A minimal, structurally valid JWT for E2E tests.
 * The payload claims don't need to be real — the backend is fully intercepted.
 * Exported here so api-routes.ts can return it from the auth/refresh intercept.
 *
 * Payload: { sub, exp, ial: "1plus", id_proofing_completed_at: 1775000000 }
 * The IAL and id-proofing claims satisfy CO's IalGuard. DC ignores them.
 * id_proofing_completed_at 1775000000 (~Apr 2026) stays fresh for the 5-year window.
 */
export const MOCK_JWT =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
  'eyJzdWIiOiJ0ZXN0LXVzZXIiLCJleHAiOjk5OTk5OTk5OTksImlhbCI6IjFwbHVzIiwiaWRfcHJvb2ZpbmdfY29tcGxldGVkX2F0IjoxNzc1MDAwMDAwfQ.' +
  'mock-signature-not-verified-in-e2e'

export type IssuanceTypeInt = 0 | 1 | 2 | 3
export type ApplicationStatusInt = 0 | 1 | 2 | 3 | 4 | 5
export type CardStatusInt = 0 | 1 | 2 | 3

export interface MockApplication {
  applicationNumber: string
  caseNumber: string
  applicationStatus: ApplicationStatusInt
  benefitIssueDate: string
  benefitExpirationDate: string
  last4DigitsOfCard: string | null
  cardStatus: CardStatusInt
  cardRequestedAt: string | null
  cardMailedAt: string | null
  cardActivatedAt: string | null
  cardDeactivatedAt: string | null
  children: Array<{ caseNumber: number; firstName: string; lastName: string }>
  childrenOnApplication: number
  issuanceType: IssuanceTypeInt
}

export interface MockSummerEbtCase {
  summerEBTCaseID: string
  applicationId: string
  applicationStudentId: string | null
  childFirstName: string
  childLastName: string
  childDateOfBirth: string
  householdType: string
  eligibilityType: string
  applicationDate: string | null
  applicationStatus: ApplicationStatusInt | null
  ebtCaseNumber: string
  ebtCardLastFour: string | null
  ebtCardStatus: string | null
  ebtCardIssueDate: string | null
  ebtCardBalance: number | null
  benefitAvailableDate: string
  benefitExpirationDate: string
  eligibilitySource: string | null
  issuanceType: IssuanceTypeInt
}

interface MockAddress {
  streetAddress1: string
  streetAddress2: string | null
  city: string
  state: string
  postalCode: string
}

export interface MockHouseholdData {
  email: string
  phone: string
  summerEbtCases: MockSummerEbtCase[]
  applications: MockApplication[]
  addressOnFile: MockAddress
  userProfile: { firstName: string; middleName: string; lastName: string }
  benefitIssuanceType: IssuanceTypeInt
}

/** A date string well outside the 14-day cooldown window. */
export const OLD_CARD_DATE = '2025-01-01T00:00:00Z'

/** A date string within the 14-day cooldown window (today minus 1 day). */
export function recentCardDate(): string {
  const d = new Date()
  d.setDate(d.getDate() - 1)
  return d.toISOString()
}

// ─── Application factory (legacy, used by some flow tests) ─────────────────

interface ApplicationOptions {
  applicationNumber?: string
  caseNumber?: string
  issuanceType?: IssuanceTypeInt
  cardRequestedAt?: string | null
  cardStatus?: CardStatusInt
  last4DigitsOfCard?: string | null
  children?: Array<{ caseNumber: number; firstName: string; lastName: string }>
}

export function makeApplication(overrides: ApplicationOptions = {}): MockApplication {
  return {
    applicationNumber: 'APP-2026-001',
    caseNumber: 'CASE-100001',
    applicationStatus: 2, // Approved
    benefitIssueDate: '2026-01-08T00:00:00Z',
    benefitExpirationDate: '2026-09-30T00:00:00Z',
    last4DigitsOfCard: '1234',
    cardStatus: 2, // Active
    cardRequestedAt: OLD_CARD_DATE,
    cardMailedAt: '2025-01-15T00:00:00Z',
    cardActivatedAt: '2025-01-20T00:00:00Z',
    cardDeactivatedAt: null,
    children: [{ caseNumber: 456001, firstName: 'John', lastName: 'Doe' }],
    childrenOnApplication: 1,
    issuanceType: 1, // SummerEbt
    ...overrides
  }
}

// ─── SummerEbtCase factory ─────────────────────────────────────────────────

interface SummerEbtCaseOptions {
  summerEBTCaseID?: string
  applicationId?: string
  childFirstName?: string
  childLastName?: string
  childDateOfBirth?: string
  householdType?: string
  eligibilityType?: string
  ebtCaseNumber?: string
  ebtCardLastFour?: string | null
  ebtCardStatus?: string | null
  benefitAvailableDate?: string
  benefitExpirationDate?: string
  issuanceType?: IssuanceTypeInt
  /** Extra fields passed through to the spread (e.g. cardRequestedAt for cooldown tests) */
  [key: string]: unknown
}

export function makeSummerEbtCase(overrides: SummerEbtCaseOptions = {}): MockSummerEbtCase {
  const {
    summerEBTCaseID = 'SEBT-001',
    applicationId = 'APP-2026-001',
    childFirstName = 'John',
    childLastName = 'Doe',
    childDateOfBirth = '2015-06-15T00:00:00Z',
    householdType = 'SNAP',
    eligibilityType = 'Direct',
    ebtCaseNumber = 'CASE-100001',
    ebtCardLastFour = '1234',
    ebtCardStatus = 'Active',
    benefitAvailableDate = '2026-01-08T00:00:00Z',
    benefitExpirationDate = '2026-09-30T00:00:00Z',
    issuanceType = 1, // SummerEbt
    ...extra
  } = overrides

  return {
    summerEBTCaseID,
    applicationId,
    applicationStudentId: null,
    childFirstName,
    childLastName,
    childDateOfBirth,
    householdType,
    eligibilityType,
    applicationDate: null,
    applicationStatus: 2, // Approved
    ebtCaseNumber,
    ebtCardLastFour,
    ebtCardStatus,
    ebtCardIssueDate: null,
    ebtCardBalance: null,
    benefitAvailableDate,
    benefitExpirationDate,
    eligibilitySource: null,
    issuanceType,
    ...extra
  } as MockSummerEbtCase
}

// ─── HouseholdData factory ─────────────────────────────────────────────────

const ADDRESS_DEFAULTS: Record<StateCode, MockAddress> = {
  dc: {
    streetAddress1: '1350 Pennsylvania Ave NW',
    streetAddress2: 'Suite 400',
    city: 'Washington',
    state: 'DC',
    postalCode: '20004'
  },
  co: {
    streetAddress1: '200 E Colfax Ave',
    streetAddress2: null,
    city: 'Denver',
    state: 'CO',
    postalCode: '80203'
  }
}

interface HouseholdDataOptions {
  summerEbtCases?: MockSummerEbtCase[]
  applications?: MockApplication[]
  benefitIssuanceType?: IssuanceTypeInt
  addressOnFile?: MockAddress
}

export function makeHouseholdData(overrides: HouseholdDataOptions = {}): MockHouseholdData {
  return {
    email: 'test@example.com',
    phone: '(202) 555-0100',
    summerEbtCases: overrides.summerEbtCases ?? [makeSummerEbtCase()],
    applications: overrides.applications ?? [makeApplication()],
    addressOnFile: overrides.addressOnFile ?? ADDRESS_DEFAULTS[currentState],
    userProfile: { firstName: 'Jane', middleName: 'M', lastName: 'Doe' },
    benefitIssuanceType: overrides.benefitIssuanceType ?? 1
  }
}

export const DEFAULT_FEATURE_FLAGS = {
  enable_enrollment_status: true,
  enable_card_replacement: true,
  enable_spanish_support: true,
  show_application_number: true,
  show_case_number: true,
  show_card_last4: true
}
