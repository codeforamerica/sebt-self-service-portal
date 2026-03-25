/**
 * Factory functions for mock API responses used in card-replacement E2E tests.
 *
 * Integer enum values match what the real .NET backend returns.
 * The frontend Zod schema preprocesses them to string enums before use.
 *
 * IssuanceType: 0=Unknown, 1=SummerEbt, 2=TanfEbtCard, 3=SnapEbtCard
 * ApplicationStatus: 0=Unknown, 1=Pending, 2=Approved, 3=Denied, 4=UnderReview, 5=Cancelled
 * CardStatus: 0=Unknown, 1=Requested, 2=Mailed, 3=Active, 4=Deactivated
 */

/**
 * A minimal, structurally valid JWT for E2E tests.
 * The payload claims don't need to be real — the backend is fully intercepted.
 * Exported here so api-routes.ts can return it from the auth/refresh intercept.
 */
export const MOCK_JWT =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
  'eyJzdWIiOiJ0ZXN0LXVzZXIiLCJleHAiOjk5OTk5OTk5OTl9.' +
  'mock-signature-not-verified-in-e2e'

export type IssuanceTypeInt = 0 | 1 | 2 | 3
export type ApplicationStatusInt = 0 | 1 | 2 | 3 | 4 | 5
export type CardStatusInt = 0 | 1 | 2 | 3 | 4

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

export interface MockHouseholdData {
  email: string
  phone: string
  applications: MockApplication[]
  addressOnFile: {
    streetAddress1: string
    streetAddress2: string | null
    city: string
    state: string
    postalCode: string
  }
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
    cardStatus: 3, // Active
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

interface HouseholdDataOptions {
  applications?: MockApplication[]
  benefitIssuanceType?: IssuanceTypeInt
}

export function makeHouseholdData(overrides: HouseholdDataOptions = {}): MockHouseholdData {
  return {
    email: 'test@example.com',
    phone: '(202) 555-0100',
    applications: overrides.applications ?? [makeApplication()],
    addressOnFile: {
      streetAddress1: '123 Main Street',
      streetAddress2: 'Apt 4B',
      city: 'Washington',
      state: 'DC',
      postalCode: '20001'
    },
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
