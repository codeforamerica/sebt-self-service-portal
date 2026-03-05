/**
 * Off-Boarding Page Unit Tests (Co-located)
 *
 * Tests the server component behavior including:
 * - searchParams.canApply parsing (defaults to true, 'false' yields false)
 * - State-specific contactHref from getStateLinks
 * - Translation key mapping to OffBoardingContent props
 */
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/lib/state', () => ({
  getState: vi.fn().mockReturnValue('dc')
}))

vi.mock('@/lib/links', () => ({
  getStateLinks: vi.fn().mockReturnValue({
    help: { contactUs: 'https://sunbucks.dc.gov/page/contact-us' }
  })
}))

vi.mock('@/lib/translations', () => ({
  getTranslations: vi.fn().mockImplementation(() => {
    return (key: string) => `offBoarding:${key}`
  })
}))

vi.mock('@/features/auth', () => ({
  OffBoardingContent: (props: Record<string, unknown>) => (
    <div
      data-testid="off-boarding-content"
      data-can-apply={String(props.canApply)}
      data-contact-href={String(props.contactHref)}
      data-title={String(props.title)}
      data-body={String(props.body)}
      data-contact-label={String(props.contactLabel)}
      data-back-href={String(props.backHref)}
    />
  )
}))

import { getStateLinks } from '@/lib/links'
import { getState } from '@/lib/state'
import OffBoardingPage from './page'

const mockGetState = vi.mocked(getState)
const mockGetStateLinks = vi.mocked(getStateLinks)

async function renderPage(searchParams: { canApply?: string } = {}) {
  const page = await OffBoardingPage({
    searchParams: Promise.resolve(searchParams)
  })
  return render(page)
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
    it('defaults canApply to true when searchParams has no canApply', async () => {
      await renderPage({})

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-can-apply', 'true')
    })

    it('sets canApply to true when searchParams.canApply is "true"', async () => {
      await renderPage({ canApply: 'true' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-can-apply', 'true')
    })

    it('sets canApply to false when searchParams.canApply is "false"', async () => {
      await renderPage({ canApply: 'false' })

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-can-apply', 'false')
    })
  })

  describe('State-specific props', () => {
    it('passes the contactHref from state links', async () => {
      await renderPage()

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute(
        'data-contact-href',
        'https://sunbucks.dc.gov/page/contact-us'
      )
    })

    it('calls getStateLinks with the current state', async () => {
      mockGetState.mockReturnValue('co')

      await renderPage()

      expect(mockGetStateLinks).toHaveBeenCalledWith('co')
    })

    it('falls back to helpDeskEmail when contactUs is a placeholder', async () => {
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

      await renderPage()

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute(
        'data-contact-href',
        'mailto:cdhs_sebt_supportcenter@state.co.us'
      )
    })
  })

  describe('Translation key mapping', () => {
    it('maps translation keys to the correct props', async () => {
      await renderPage()

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-title', 'offBoarding:title')
      expect(content).toHaveAttribute('data-body', 'offBoarding:body1')
      expect(content).toHaveAttribute('data-contact-label', 'offBoarding:action1')
    })
  })

  describe('Static props', () => {
    it('passes backHref pointing to the id-proofing form', async () => {
      await renderPage()

      const content = screen.getByTestId('off-boarding-content')
      expect(content).toHaveAttribute('data-back-href', '/login/id-proofing')
    })
  })
})
