/**
 * MSW Request Handlers
 *
 * Define mock API responses for testing.
 * These handlers mock the backend API for unit and integration tests.
 */
import { delay, http, HttpResponse } from 'msw'

import type { RequestOtpRequest, ValidateOtpRequest } from '@/features/auth'

// Test email addresses for different scenarios
export const TEST_EMAILS = {
  success: 'test@example.com',
  rateLimit: 'ratelimit@example.com',
  notFound: 'notfound@example.com',
  serverError: 'error@example.com',
  badRequest: 'badrequest@example.com'
} as const

// Test OTP codes
export const TEST_OTP = {
  valid: '123456',
  expired: '000000',
  invalid: '999999'
} as const

// Test feature flags (SUN Bucks portal features)
export const TEST_FEATURE_FLAGS = {
  enable_enrollment_status: true,
  enable_card_replacement: false,
  enable_spanish_support: true
} as const

// Test household data (mirrors MockHouseholdRepository seeded data)
// Updated to match PR #33 nested applications[] structure
// issuanceType values: 0=Unknown, 1=SummerEbt, 2=TanfEbtCard, 3=SnapEbtCard
export const TEST_HOUSEHOLD_DATA = {
  email: 'test@example.com',
  phone: '(303) 555-0100',
  benefitIssuanceType: 3, // SnapEbtCard
  applications: [
    {
      applicationNumber: 'APP-2026-001',
      caseNumber: 'CASE-DC-2026-001',
      applicationStatus: 'Approved',
      benefitIssueDate: '2026-01-08T00:00:00Z',
      benefitExpirationDate: '2026-03-19T00:00:00Z',
      last4DigitsOfCard: '1234',
      cardStatus: 'Active',
      cardRequestedAt: '2026-01-01T00:00:00Z',
      cardMailedAt: '2026-01-03T00:00:00Z',
      cardActivatedAt: '2026-01-08T00:00:00Z',
      cardDeactivatedAt: null,
      issuanceType: 3, // SnapEbtCard
      children: [
        { caseNumber: 456001, firstName: 'Sophia', lastName: 'Martinez' },
        { caseNumber: 456002, firstName: 'James', lastName: 'Martinez' }
      ],
      childrenOnApplication: 2
    }
  ],
  addressOnFile: {
    streetAddress1: '123 Main Street',
    streetAddress2: 'Apt 4B',
    city: 'Washington',
    state: 'DC',
    postalCode: '20001'
  },
  userProfile: {
    firstName: 'Maria',
    middleName: 'L',
    lastName: 'Martinez'
  }
} as const

export const handlers = [
  // Health check endpoint
  http.get('/api/health', () => {
    return HttpResponse.json({ status: 'ok' })
  }),

  // Request OTP endpoint
  http.post('/api/auth/otp/request', async ({ request }) => {
    const body = (await request.json()) as RequestOtpRequest

    // Add small delay to allow loading state to be observable in tests
    await delay(50)

    // Validate email format
    if (!body.email || !body.email.includes('@')) {
      return HttpResponse.json({ error: 'Invalid email address' }, { status: 400 })
    }

    // Simulate rate limiting
    if (body.email === TEST_EMAILS.rateLimit) {
      return HttpResponse.json(
        { error: 'Too many requests. Please wait a moment and try again.' },
        { status: 429 }
      )
    }

    // Simulate user not found (but we still return 200 for security - no email enumeration)
    if (body.email === TEST_EMAILS.notFound) {
      return HttpResponse.json(null, { status: 204 })
    }

    // Simulate server error (500 - will trigger retries in production)
    if (body.email === TEST_EMAILS.serverError) {
      return HttpResponse.json({ error: 'Internal server error' }, { status: 500 })
    }

    // Simulate bad request error (400 - no retries)
    if (body.email === TEST_EMAILS.badRequest) {
      return HttpResponse.json({ error: 'Invalid request data' }, { status: 400 })
    }

    // Success - OTP sent
    return HttpResponse.json(null, { status: 204 })
  }),

  // Validate OTP endpoint
  http.post('/api/auth/otp/validate', async ({ request }) => {
    const body = (await request.json()) as ValidateOtpRequest

    // Add small delay to allow loading state to be observable in tests
    await delay(50)

    // Validate required fields
    if (!body.email || !body.otp) {
      return HttpResponse.json({ error: 'Email and OTP are required' }, { status: 400 })
    }

    if (!/^\d{6}$/.test(body.otp)) {
      return HttpResponse.json({ error: 'OTP must be 6 digits' }, { status: 400 })
    }

    if (body.otp === TEST_OTP.expired) {
      return HttpResponse.json(
        { error: 'OTP has expired. Please request a new one.' },
        { status: 401 }
      )
    }

    if (body.otp === TEST_OTP.invalid || body.otp !== TEST_OTP.valid) {
      return HttpResponse.json({ error: 'Invalid OTP. Please try again.' }, { status: 401 })
    }

    // Success - return mock token
    return HttpResponse.json({
      token: 'mock-jwt-token-for-testing'
    })
  }),

  // Refresh token endpoint
  http.post('/api/auth/refresh', async ({ request }) => {
    await delay(50)

    // Check for Authorization header (requires valid token)
    const authHeader = request.headers.get('Authorization')
    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    // Success - return new mock token
    return HttpResponse.json({
      token: 'mock-jwt-token-refreshed'
    })
  }),

  // Feature flags endpoint
  http.get('/api/features', async () => {
    // Add small delay to allow loading state to be observable in tests
    await delay(50)

    return HttpResponse.json(TEST_FEATURE_FLAGS)
  }),

  // Household data endpoint
  http.get('/api/household/data', async () => {
    await delay(50)

    return HttpResponse.json(TEST_HOUSEHOLD_DATA)
  })
]
