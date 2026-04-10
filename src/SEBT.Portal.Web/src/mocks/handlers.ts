/**
 * MSW Request Handlers
 *
 * Define mock API responses for testing.
 * These handlers mock the backend API for unit and integration tests.
 */
import { delay, http, HttpResponse } from 'msw'

import type { RequestOtpRequest } from '@/features/auth/api/request-otp/schema'
import type { SubmitIdProofingRequest } from '@/features/auth/api/submit-id-proofing/schema'
import type { ValidateOtpRequest } from '@/features/auth/api/validate-otp/schema'

// Test email addresses for different scenarios
export const TEST_EMAILS = {
  success: 'test@example.com',
  rateLimit: 'ratelimit@example.com',
  notFound: 'notfound@example.com',
  serverError: 'error@example.com',
  badRequest: 'badrequest@example.com',
  // ID proofing result: documentVerificationRequired with challengeId
  docVerifyRequired: 'docverify@example.com',
  // ID proofing result: failed with offboarding reason
  docVerifyFailed: 'docverifyfail@example.com'
} as const

// Test OTP codes
export const TEST_OTP = {
  valid: '123456',
  expired: '000000',
  invalid: '999999'
} as const

// Centralizes the Set-Cookie shape across mock auth handlers so a future tweak
// (e.g. SameSite=Strict) only happens in one place. Pass an empty value + past
// expiry to simulate the logout delete instruction.
function sessionCookie(value: string, expires?: string): string {
  const base = `sebt_portal_session=${value}; HttpOnly; Secure; SameSite=Lax; Path=/`
  return expires ? `${base}; Expires=${expires}` : base
}

// Test feature flags (SUN Bucks portal features)
export const TEST_FEATURE_FLAGS = {
  enable_enrollment_status: true,
  enable_card_replacement: false,
  enable_spanish_support: true,
  show_application_number: true,
  show_case_number: true,
  show_card_last4: true
} as const

// Test household data (mirrors MockHouseholdRepository seeded data)
// Updated to match PR #33 nested applications[] structure
// issuanceType values: 0=Unknown, 1=SummerEbt, 2=TanfEbtCard, 3=SnapEbtCard
export const TEST_HOUSEHOLD_DATA = {
  email: 'test@example.com',
  phone: '3035550100',
  benefitIssuanceType: 3, // SnapEbtCard
  summerEbtCases: [
    {
      summerEBTCaseID: 'SEBT-001',
      childFirstName: 'Sophia',
      childLastName: 'Martinez',
      householdType: 'OSSE',
      eligibilityType: 'NSLP',
      issuanceType: 1,
      ebtCardLastFour: '1234',
      ebtCardStatus: 'Active',
      benefitAvailableDate: '2026-06-01T00:00:00Z',
      benefitExpirationDate: '2026-08-31T00:00:00Z'
    },
    {
      summerEBTCaseID: 'SEBT-002',
      childFirstName: 'James',
      childLastName: 'Martinez',
      householdType: 'OSSE',
      eligibilityType: 'NSLP',
      issuanceType: 1
    }
  ],
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
    streetAddress1: '1350 Pennsylvania Ave NW',
    streetAddress2: 'Suite 400',
    city: 'Washington',
    state: 'DC',
    postalCode: '20004'
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

    // Success — backend sets HttpOnly session cookie; session shape is read from /auth/status.
    return new HttpResponse(null, {
      status: 204,
      headers: { 'Set-Cookie': sessionCookie('mock-jwt-token-for-testing') }
    })
  }),

  // Refresh session cookie
  http.post('/api/auth/refresh', async ({ request }) => {
    await delay(50)

    // Require an existing session cookie to refresh.
    const cookie = request.headers.get('Cookie') ?? ''
    if (!cookie.includes('sebt_portal_session=')) {
      return HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
    }

    // Rotate the cookie — session contents unchanged in this mock.
    return new HttpResponse(null, {
      status: 204,
      headers: { 'Set-Cookie': sessionCookie('mock-jwt-token-refreshed') }
    })
  }),

  // Session status — default mock returns an authenticated user so form-submission
  // tests that call login() after OTP/OIDC succeed without needing to round-trip a
  // Set-Cookie header through MSW (which does not maintain a cookie jar in Node).
  // Unauthenticated tests override via server.use() to return 401.
  http.get('/api/auth/status', () => {
    return HttpResponse.json({
      isAuthorized: true,
      email: TEST_EMAILS.success,
      ial: '1',
      idProofingStatus: 0,
      idProofingCompletedAt: null,
      idProofingExpiresAt: null
    })
  }),

  // Logout — clears the session cookie.
  http.post('/api/auth/logout', () => {
    return new HttpResponse(null, {
      status: 204,
      headers: { 'Set-Cookie': sessionCookie('', 'Thu, 01 Jan 1970 00:00:00 GMT') }
    })
  }),

  // OIDC CO config (public; for frontend PKCE flow)
  http.get('/api/auth/oidc/co/config', () => {
    return HttpResponse.json({
      authorizationEndpoint: 'https://auth.example.com/authorize',
      tokenEndpoint: 'https://auth.example.com/token',
      clientId: 'test-client',
      redirectUri: 'http://localhost:3000/callback',
      languageParam: 'en'
    })
  }),

  // OIDC callback (Next.js: exchange + validate; returns callbackToken for complete-login)
  http.post('/api/auth/oidc/callback', async ({ request }) => {
    const body = (await request.json()) as {
      code?: string
      code_verifier?: string
      stateCode?: string
    }
    const currentState = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase()
    if (!body?.code || !body?.code_verifier || body?.stateCode !== currentState) {
      return HttpResponse.json(
        {
          error: 'Missing or invalid code, code_verifier, or stateCode (must match current state).'
        },
        { status: 400 }
      )
    }
    return HttpResponse.json({ callbackToken: 'mock-callback-token-for-testing' })
  }),

  // OIDC complete-login (.NET: validates callbackToken, creates session via HttpOnly cookie)
  // Response shape mirrors .NET's CompleteLoginResponse: `returnUrl` is always present,
  // null for normal login, a safe relative path for step-up. System.Text.Json defaults
  // serialize nullable properties as `null`, not omitted — tests must mirror this so
  // Zod schema drift (nullish vs optional) is caught at unit level.
  http.post('/api/auth/oidc/complete-login', async ({ request }) => {
    const body = (await request.json()) as { stateCode?: string; callbackToken?: string }
    if (!body?.stateCode || !body?.callbackToken) {
      return HttpResponse.json({ error: 'Missing stateCode or callbackToken.' }, { status: 400 })
    }
    return HttpResponse.json(
      { returnUrl: null },
      {
        status: 200,
        headers: { 'Set-Cookie': sessionCookie('mock-jwt-token-for-testing') }
      }
    )
  }),

  // Feature flags endpoint
  http.get('/api/features', async () => {
    // Add small delay to allow loading state to be observable in tests
    await delay(50)

    return HttpResponse.json(TEST_FEATURE_FLAGS)
  }),

  // ID proofing endpoint — returns result with optional challenge context.
  // Default returns 'matched'. Tests override via server.use() for other scenarios.
  http.post('/api/id-proofing', async ({ request }) => {
    const body = (await request.json()) as SubmitIdProofingRequest

    await delay(50)

    if (!body.dateOfBirth?.month || !body.dateOfBirth?.day || !body.dateOfBirth?.year) {
      return HttpResponse.json({ error: 'Date of birth is required' }, { status: 400 })
    }

    // Simulate failure when user selects "none of the above" for ID type
    if (body.idType === null) {
      return HttpResponse.json({ result: 'failed', canApply: true })
    }

    // Simulate step-up failure (canApply: false) with Medicaid ID for dev testing.
    // In production, the backend determines canApply based on the user's enrollment pathway.
    if (body.idType === 'medicaidId') {
      return HttpResponse.json({ result: 'failed', canApply: false })
    }

    // Default: identity matched
    return HttpResponse.json({ result: 'matched' })
  }),

  // Challenge start endpoint — returns JIT Socure token (D2)
  http.get('/api/challenges/:id/start', async () => {
    await delay(50)

    return HttpResponse.json({
      docvTransactionToken: 'mock-token-for-testing',
      docvUrl: 'https://websdk.socure.com'
    })
  }),

  // Verification status endpoint — first call returns pending, subsequent returns verified.
  // Uses a closure counter to simulate async verification (D3).
  // allowIdRetry is included so the interstitial can show/hide the "Enter an ID number" button (D9).
  (() => {
    let callCount = 0
    return http.get('/api/id-proofing/status', async () => {
      await delay(50)
      callCount++
      if (callCount <= 1) {
        return HttpResponse.json({ status: 'pending', allowIdRetry: false })
      }
      callCount = 0 // Reset for next test
      return HttpResponse.json({ status: 'verified', allowIdRetry: false })
    })
  })(),

  // Household data endpoint
  http.get('/api/household/data', async () => {
    await delay(50)

    return HttpResponse.json(TEST_HOUSEHOLD_DATA)
  }),

  // Address update endpoint
  // TODO: Tests override via server.use() for validation scenarios (422 invalid/suggestion)
  http.put('/api/household/address', () => {
    return HttpResponse.json({ status: 'valid' }, { status: 200 })
  }),

  // Card replacement endpoint (stub — no real persistence yet)
  // TODO: When state connector persistence is wired up, update this handler to
  // reflect the real contract (cooldown validation errors, etc.)
  http.post('/api/household/cards/replace', () => {
    return new HttpResponse(null, { status: 204 })
  }),

  // Card replacement endpoint (stub — no real persistence yet)
  // TODO: When state connector persistence is wired up, update this handler to
  // reflect the real contract (cooldown validation errors, etc.)
  http.post('/api/household/cards/replace', () => {
    return new HttpResponse(null, { status: 204 })
  })
]
