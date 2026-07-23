/**
 * Factory functions for mock API responses used in card-replacement E2E tests.
 *
 * Integer enum values match what the real .NET backend returns.
 * The frontend Zod schema preprocesses them to string enums before use.
 *
 * IssuanceType: 0=Unknown, 1=SummerEbt, 2=TanfEbtCard, 3=SnapEbtCard
 * ApplicationStatus: 0=Unknown, 1=Pending, 2=Approved, 3=Denied, 4=UnderReview, 5=Cancelled
 *
 * Card lifecycle fields live on SummerEbtCase, not Application — see DC-256.
 */

import { currentState, type StateCode } from './state'

/**
 * Opaque session-cookie value used by injectAuth() in E2E tests.
 * The backend is fully intercepted (setupApiRoutes mocks /auth/status with the
 * authenticated session shape), so the token itself is never decoded or validated —
 * any non-empty string would work. Kept JWT-shaped for clarity in network logs.
 */
export const MOCK_JWT = 'e2e-mock-session-token'

/** Stable portal user UUID for E2E auth/status mocks (required for user-scoped household cache). */
export const MOCK_USER_ID = '018f0000-0000-7000-8000-0000000000e2'

export type IssuanceTypeInt = 0 | 1 | 2 | 3
export type ApplicationStatusInt = 0 | 1 | 2 | 3 | 4 | 5

export interface MockApplication {
  applicationNumber: string
  caseNumber: string
  applicationStatus: ApplicationStatusInt
  benefitIssueDate: string
  benefitExpirationDate: string
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
  /** When set, dashboard SEBT ID row shows this instead of ebtCaseNumber (state connector contract). */
  caseDisplayNumber?: string | null
  ebtCardLastFour: string | null
  ebtCardStatus: string | null
  ebtCardIssueDate: string | null
  ebtCardBalance: number | null
  benefitAvailableDate: string
  benefitExpirationDate: string
  eligibilitySource: string | null
  issuanceType: IssuanceTypeInt
  allowAddressChange: boolean
  allowCardReplacement: boolean
}

interface MockAddress {
  streetAddress1: string
  streetAddress2: string | null
  city: string
  state: string
  postalCode: string
}

export interface MockAllowedActions {
  canUpdateAddress: boolean
  canRequestReplacementCard: boolean
  addressUpdateDeniedMessageKey?: string | null
  cardReplacementDeniedMessageKey?: string | null
}

export interface MockHouseholdData {
  email: string
  phone: string
  summerEbtCases: MockSummerEbtCase[]
  applications: MockApplication[]
  addressOnFile: MockAddress
  userProfile: { firstName: string; middleName: string; lastName: string }
  benefitIssuanceType: IssuanceTypeInt
  /** Backend enum: 0=NonCoLoaded, 1=CoLoadedOnly, 2=MixedOrApplicantExcluded */
  coLoadedCohort?: 0 | 1 | 2
  allowedActions?: MockAllowedActions
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
  children?: Array<{ caseNumber: number; firstName: string; lastName: string }>
}

export function makeApplication(overrides: ApplicationOptions = {}): MockApplication {
  return {
    applicationNumber: 'APP-2026-001',
    caseNumber: 'CASE-100001',
    applicationStatus: 2, // Approved
    benefitIssueDate: '2026-01-08T00:00:00Z',
    benefitExpirationDate: '2026-09-30T00:00:00Z',
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
    allowAddressChange: true,
    allowCardReplacement: true,
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
  coLoadedCohort?: 0 | 1 | 2
  allowedActions?: MockAllowedActions
  userProfile?: { firstName: string; middleName: string; lastName: string }
}

/** Fully co-loaded SNAP household: self-service address/card actions denied. */
export const CO_LOADED_ONLY_ALLOWED_ACTIONS: MockAllowedActions = {
  canUpdateAddress: false,
  canRequestReplacementCard: false
}

export function makeHouseholdData(overrides: HouseholdDataOptions = {}): MockHouseholdData {
  return {
    email: 'test@example.com',
    phone: '(202) 555-0100',
    summerEbtCases: overrides.summerEbtCases ?? [makeSummerEbtCase()],
    applications: overrides.applications ?? [makeApplication()],
    addressOnFile: overrides.addressOnFile ?? ADDRESS_DEFAULTS[currentState],
    userProfile: overrides.userProfile ?? { firstName: 'Jane', middleName: 'M', lastName: 'Doe' },
    benefitIssuanceType: overrides.benefitIssuanceType ?? 1,
    coLoadedCohort: overrides.coLoadedCohort ?? 0,
    ...(overrides.allowedActions !== undefined ? { allowedActions: overrides.allowedActions } : {})
  }
}

/** SNAP co-loaded-only household used by DC co-loaded info-page / dashboard E2E. */
export function makeCoLoadedOnlyHousehold(overrides: HouseholdDataOptions = {}): MockHouseholdData {
  return makeHouseholdData({
    benefitIssuanceType: 3, // SnapEbtCard
    coLoadedCohort: 1, // CoLoadedOnly
    allowedActions: CO_LOADED_ONLY_ALLOWED_ACTIONS,
    summerEbtCases: [
      makeSummerEbtCase({
        summerEBTCaseID: 'SNAP-CO-001',
        childFirstName: 'Sophia',
        childLastName: 'Martinez',
        issuanceType: 3,
        eligibilityType: 'SNAP',
        ebtCaseNumber: 'SNAP-CO-001',
        allowAddressChange: false,
        allowCardReplacement: false
      })
    ],
    ...overrides
  })
}

export const DEFAULT_FEATURE_FLAGS = {
  enable_enrollment_status: true,
  enable_card_replacement: true,
  enable_spanish_support: true,
  show_application_number: true,
  show_case_number: true,
  show_card_last4: true,
  outage_page_enabled: false
}
