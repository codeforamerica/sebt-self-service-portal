/**
 * Off-Boarding Page Unit Tests (Co-located)
 *
 * Covers:
 * - searchParams.canApply parsing (defaults to true, 'false' yields false)
 * - State-specific contactHref from getStateLinks
 * - Translation key mapping branches by session.isCoLoaded
 *   (co-loaded users see the "cannot identify" copy, not DocV copy)
 */
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@sebt/design-system', () => ({
  getState: vi.fn().mockReturnValue('dc'),
  getStateLinks: vi.fn().mockReturnValue({
    help: { contactUs: 'https://sunbucks.dc.gov/page/contact-us' }
  })
}))

// Keys the test asserts against get a populated value; anything else (state-specific
// keys that may be empty in some locales) returns empty so the page's `|| fallback`
// branch is exercised.
const POPULATED_KEYS = new Set([
  'offBoarding:title',
  'offBoarding:body1',
  'offBoarding:body2',
  'offBoarding:body3',
  'offBoarding:action',
  'offBoarding:action2',
  'offBoarding:coLoadedTitle',
  'offBoarding:coLoadedBody1',
  'offBoarding:coLoadedAction1',
  'offBoarding:coLoadedBody2',
  'offBoarding:coLoadedAction2',
  'offBoarding:docVerificationFailedTitle',
  'offBoarding:docVerificationFailedBody',
  'stepUpFailure:title',
  'stepUpFailure:body',
  'common:linkContactUs',
  'common:back',
  'common:continue',
  'dashboard:alertApplicationsTitle',
  'dashboard:alertApplicationsBody',
  'dashboard:alertApplicationsAction'
])
let emptyKeys = new Set<string>()
vi.mock('react-i18next', () => ({
  useTranslation: (ns: string) => ({
    t: (key: string, defaultValue?: string) => {
      const fullKey = `${ns}:${key}`
      if (emptyKeys.has(fullKey)) return defaultValue ?? fullKey
      if (POPULATED_KEYS.has(fullKey)) return fullKey
      return defaultValue ?? fullKey
    },
    i18n: { language: 'en' }
  })
}))

const mockSearchParams = new Map<string, string>()
vi.mock('next/navigation', () => ({
  useSearchParams: () => ({
    get: (key: string) => mockSearchParams.get(key) ?? null
  })
}))

const mockUseAuth = vi.fn()
vi.mock('@/features/auth', () => ({
  useAuth: () => mockUseAuth(),
  OffBoardingContent: (props: Record<string, unknown>) => (
    <div
      data-testid="off-boarding-content"
      data-can-apply={String(props.canApply)}
      data-contact-href={String(props.contactHref)}
      data-title={String(props.title)}
      data-body={String(props.body)}
      data-contact-label={String(props.contactLabel)}
      data-apply-body={String(props.applyBody)}
      data-apply-label={String(props.applyLabel)}
      data-back-href={String(props.backHref)}
      data-back-label={String(props.backLabel)}
      data-body-list={String(props.bodyList)}
      data-body-note={String(props.bodyNote)}
      data-continue-href={String(props.continueHref)}
      data-continue-label={String(props.continueLabel)}
    />
  )
}))

import { getState, getStateLinks } from '@sebt/design-system'

import OffBoardingPage from './page'

const mockGetState = vi.mocked(getState)
const mockGetStateLinks = vi.mocked(getStateLinks)

function renderPage(
  opts: { canApply?: string; reason?: string; isCoLoaded?: boolean; emptyKeys?: string[] } = {}
) {
  mockSearchParams.clear()
  if (opts.canApply !== undefined) mockSearchParams.set('canApply', opts.canApply)
  if (opts.reason !== undefined) mockSearchParams.set('reason', opts.reason)
  mockUseAuth.mockReturnValue({
    session: { isCoLoaded: opts.isCoLoaded ?? false }
  })
  emptyKeys = new Set(opts.emptyKeys ?? [])
  return render(<OffBoardingPage />)
}

describe('OffBoardingPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetState.mockReturnValue('dc')
    mockGetStateLinks.mockReturnValue({
      help: { contactUs: 'https://sunbucks.dc.gov/page/contact-us', faqs: '' },
      footer: {
        publicNotifications: '',
        accessibility: '',
        privacyAndSecurity: '',
        googleTranslateDisclaimer: '',
        about: '',
        termsAndConditions: ''
      },
      external: { contactUsAssistance: '' }
    })
  })

  describe('searchParams.canApply parsing', () => {
    it('defaults canApply to true when searchParams has no canApply', () => {
      renderPage({})

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-can-apply', 'true')
    })

    it('sets canApply to true when searchParams.canApply is "true"', () => {
      renderPage({ canApply: 'true' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-can-apply', 'true')
    })

    it('sets canApply to false when searchParams.canApply is "false"', () => {
      renderPage({ canApply: 'false' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-can-apply', 'false')
    })
  })

  describe('State-specific props', () => {
    it('passes the contactHref from state links', () => {
      renderPage()

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute(
        'data-contact-href',
        'https://sunbucks.dc.gov/page/contact-us'
      )
    })

    it('calls getStateLinks with the current state', () => {
      mockGetState.mockReturnValue('co')

      renderPage()

      expect(mockGetStateLinks).toHaveBeenCalledWith('co')
    })

    it('falls back to helpDeskEmail when contactUs is a placeholder', () => {
      mockGetState.mockReturnValue('co')
      mockGetStateLinks.mockReturnValue({
        help: {
          contactUs: '#',
          faqs: '#',
          helpDeskEmail: 'mailto:cdhs_sebt_supportcenter@state.co.us'
        },
        footer: {
          publicNotifications: '',
          accessibility: '',
          privacyAndSecurity: '',
          googleTranslateDisclaimer: '',
          about: '',
          termsAndConditions: ''
        },
        external: { contactUsAssistance: '' }
      })

      renderPage()

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute(
        'data-contact-href',
        'mailto:cdhs_sebt_supportcenter@state.co.us'
      )
    })
  })

  describe('Translation key mapping', () => {
    it('uses the default off-boarding copy when the session is not co-loaded', () => {
      renderPage({ isCoLoaded: false })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:title')
      expect(content).toHaveAttribute('data-body', 'offBoarding:body1')
      // body2/body3 are now core body content (list + note), not the apply section
      expect(content).toHaveAttribute('data-body-list', 'offBoarding:body2')
      expect(content).toHaveAttribute('data-body-note', 'offBoarding:body3')
      expect(content).toHaveAttribute('data-apply-body', 'undefined')
    })

    it('passes a Continue primary action to /login/id-proofing on the generic screen', () => {
      renderPage({ isCoLoaded: false })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-continue-href', '/login/id-proofing')
      expect(content).toHaveAttribute('data-continue-label', 'common:continue')
    })

    it('uses the co-loaded off-boarding copy when the session is co-loaded', () => {
      renderPage({ isCoLoaded: true })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:coLoadedTitle')
      expect(content).toHaveAttribute('data-body', 'offBoarding:coLoadedBody1')
      expect(content).toHaveAttribute('data-contact-label', 'offBoarding:coLoadedAction1')
      expect(content).toHaveAttribute('data-apply-body', 'offBoarding:coLoadedBody2')
      expect(content).toHaveAttribute('data-apply-label', 'offBoarding:coLoadedAction2')
    })

    it('uses the co-loaded off-boarding copy when reason is coLoadedOnly', () => {
      renderPage({ isCoLoaded: false, reason: 'coLoadedOnly' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:coLoadedTitle')
      expect(content).toHaveAttribute('data-body', 'offBoarding:coLoadedBody1')
      expect(content).toHaveAttribute('data-contact-label', 'offBoarding:coLoadedAction1')
    })
  })

  describe('Static props', () => {
    it('passes backHref pointing to the id-proofing form (NOT Socure) for the Socure flow', () => {
      renderPage({ isCoLoaded: false })
      expect(screen.getByTestId('off-boarding-content')).toHaveAttribute(
        'data-back-href',
        '/login/id-proofing'
      )
    })

    it('passes backHref pointing to the id-proofing form (NOT Socure) for the co-loaded flow', () => {
      renderPage({ isCoLoaded: true })
      expect(screen.getByTestId('off-boarding-content')).toHaveAttribute(
        'data-back-href',
        '/login/id-proofing'
      )
    })

    it('passes the state-specific back label from the offBoarding action key when present (DC)', () => {
      renderPage({ isCoLoaded: true })
      expect(screen.getByTestId('off-boarding-content')).toHaveAttribute(
        'data-back-label',
        'offBoarding:action'
      )
    })

    it('falls back to the common "Back" label when the state omits offBoarding action (CO)', () => {
      renderPage({ isCoLoaded: true, emptyKeys: ['offBoarding:action'] })
      expect(screen.getByTestId('off-boarding-content')).toHaveAttribute(
        'data-back-label',
        'common:back'
      )
    })
  })

  describe('searchParams.reason branching', () => {
    it('renders noIdProvided-specific title and body when reason is noIdProvided', async () => {
      await renderPage({ reason: 'noIdProvided' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'We need an ID to verify you')
      expect(content).toHaveAttribute(
        'data-body',
        "To confirm your identity, we need one of the listed IDs. If you don't have any of these IDs, contact us for help."
      )
    })

    it('forces canApply to false for noIdProvided regardless of query param', async () => {
      await renderPage({ reason: 'noIdProvided', canApply: 'true' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-can-apply', 'false')
    })

    it('falls back to generic offBoarding copy when reason is absent', async () => {
      await renderPage({})

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:title')
      expect(content).toHaveAttribute('data-body', 'offBoarding:body1')
    })

    it('falls back to generic copy for unknown reasons', async () => {
      await renderPage({ reason: 'somethingElse' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:title')
    })

    it('renders docVerificationEgregiousFailed-specific copy', async () => {
      await renderPage({ reason: 'docVerificationEgregiousFailed' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:docVerificationFailedTitle')
      expect(content).toHaveAttribute('data-body', 'offBoarding:docVerificationFailedBody')
      expect(content).toHaveAttribute('data-can-apply', 'false')
    })

    it('routes docVerificationFailed to the generic "keep your account safe" screen with Continue', async () => {
      await renderPage({ reason: 'docVerificationFailed' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:title')
      expect(content).toHaveAttribute('data-body', 'offBoarding:body1')
      expect(content).toHaveAttribute('data-continue-href', '/login/id-proofing')
      expect(content).toHaveAttribute('data-continue-label', 'common:continue')
    })

    it('uses step-up failure copy for OIDC callback errors (CO MyCO step-up)', async () => {
      await renderPage({ reason: 'oidcCallbackError', isCoLoaded: false })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'stepUpFailure:title')
      expect(content).toHaveAttribute('data-body', 'stepUpFailure:body')
      expect(content).toHaveAttribute('data-back-href', '/dashboard')
      expect(content).toHaveAttribute('data-contact-label', 'common:linkContactUs')
      expect(content).toHaveAttribute('data-can-apply', 'false')
    })

    it('OIDC callback error reason wins over co-loaded session copy', async () => {
      await renderPage({ reason: 'oidcCallbackError', isCoLoaded: true })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'stepUpFailure:title')
    })

    it('uses dashboard application-alert copy when reason is noQualifyingHousehold', async () => {
      await renderPage({ reason: 'noQualifyingHousehold' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'dashboard:alertApplicationsTitle')
      expect(content).toHaveAttribute('data-body', 'dashboard:alertApplicationsBody')
      expect(content).toHaveAttribute('data-apply-label', 'dashboard:alertApplicationsAction')
      expect(content).toHaveAttribute('data-back-href', '/login/id-proofing')
      expect(content).toHaveAttribute('data-can-apply', 'true')
    })
  })
})
