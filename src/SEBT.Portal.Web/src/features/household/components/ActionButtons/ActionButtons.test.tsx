import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { ActionButtons } from './ActionButtons'

vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: vi.fn().mockReturnValue('dc')
  }
})

const { getState } = await import('@sebt/design-system')
const mockGetState = vi.mocked(getState)

describe('ActionButtons', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetState.mockReturnValue('dc')
  })

  it('renders navigation element with aria-label', () => {
    render(<ActionButtons />)

    const nav = screen.getByRole('navigation')
    expect(nav).toHaveAttribute('aria-label', 'Quick actions')
  })

  it('renders all action buttons', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(5)
  })

  it('renders check existing cards button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationCheckExistingCards
    const link = screen.getByText('Check existing cards')
    expect(link).toHaveAttribute('href', '/cards')
  })

  it('renders request new cards button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationOrderReplacementCards
    const link = screen.getByText('Request new cards')
    expect(link).toHaveAttribute('href', '/cards/request')
  })

  it('renders change mailing address button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationChangeMyMailingAddress
    const link = screen.getByText('Change my mailing address')
    expect(link).toHaveAttribute('href', '/profile/address')
  })

  it('renders change contact info button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationChangeMyContactInformation
    const link = screen.getByText('Change my contact information')
    expect(link).toHaveAttribute('href', '/profile/contact')
  })

  it('renders check applications button', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationCheckExistingApplications
    const link = screen.getByText('Check existing applications')
    expect(link).toHaveAttribute('href', '/applications')
  })

  it('renders "I want to" heading', () => {
    render(<ActionButtons />)

    // i18n key: actionNavigationLead
    expect(screen.getByText('I want to')).toBeInTheDocument()
  })

  it('renders pill-shaped buttons', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    links.forEach((link) => {
      expect(link).toHaveClass('radius-pill')
    })
  })

  it('renders chevron icon after each button text', () => {
    render(<ActionButtons />)

    const links = screen.getAllByRole('link')
    links.forEach((link) => {
      const svg = link.querySelector('svg')
      expect(svg).toBeInTheDocument()
      expect(svg).toHaveAttribute('aria-hidden', 'true')
    })
  })

  describe('DC state styling', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('dc')
    })

    it('renders buttons with secondary background and ink text', () => {
      render(<ActionButtons />)

      const links = screen.getAllByRole('link')
      links.forEach((link) => {
        expect(link).toHaveClass('bg-secondary')
        expect(link).toHaveClass('text-ink')
      })
    })
  })

  describe('CO state styling', () => {
    beforeEach(() => {
      mockGetState.mockReturnValue('co')
    })

    it('renders buttons with primary background and white text', () => {
      render(<ActionButtons />)

      const links = screen.getAllByRole('link')
      links.forEach((link) => {
        expect(link).toHaveClass('bg-primary')
        expect(link).toHaveClass('text-white')
      })
    })
  })
})
