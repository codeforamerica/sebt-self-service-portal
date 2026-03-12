import type { Address, Application, Child, HouseholdData, UserProfile } from '../api'

/**
 * Factory functions for creating test fixtures.
 * Use these to avoid duplicating mock data across tests.
 * Only override the fields relevant to your test.
 */

export function createMockChild(overrides?: Partial<Child>): Child {
  return {
    caseNumber: 456001,
    firstName: 'Sophia',
    lastName: 'Martinez',
    ...overrides
  }
}

export function createMockApplication(overrides?: Partial<Application>): Application {
  return {
    applicationNumber: 'APP-2026-001',
    caseNumber: 'CASE-DC-2026-001',
    applicationStatus: 'Approved',
    benefitIssueDate: '2026-01-08T00:00:00Z',
    benefitExpirationDate: '2026-03-19T00:00:00Z',
    last4DigitsOfCard: '1234',
    cardStatus: 'Active',
    cardRequestedAt: null,
    cardMailedAt: null,
    cardActivatedAt: null,
    cardDeactivatedAt: null,
    children: [createMockChild()],
    childrenOnApplication: 1,
    ...overrides
  }
}

export function createMockAddress(overrides?: Partial<Address>): Address {
  return {
    streetAddress1: '123 Main Street',
    streetAddress2: 'Apt 4B',
    city: 'Washington',
    state: 'DC',
    postalCode: '20001',
    ...overrides
  }
}

export function createMockUserProfile(overrides?: Partial<UserProfile>): UserProfile {
  return {
    firstName: 'Maria',
    middleName: 'L',
    lastName: 'Martinez',
    ...overrides
  }
}

export function createMockHouseholdData(overrides?: Partial<HouseholdData>): HouseholdData {
  return {
    email: 'test@example.com',
    phone: '(303) 555-0100',
    summerEbtCases: [],
    applications: [createMockApplication()],
    addressOnFile: createMockAddress(),
    userProfile: createMockUserProfile(),
    ...overrides
  }
}
