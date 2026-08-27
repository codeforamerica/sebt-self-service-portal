import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { i18n } from '@sebt/design-system/client'
import dcDisclaimer from '@/content/locales/en/dc/disclaimer.json'
import { DisclaimerPage } from './DisclaimerPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

// Drives the season. A payload without `enrollment` is an open season, which is
// what these structural cases assume; the closed suite below sets it explicitly.
let mockFeatures: unknown = {}
vi.mock('@/features/maintenance/hooks/useCheckerFeatures', () => ({
  useCheckerFeatures: () => ({ data: mockFeatures })
}))

describe('DisclaimerPage', () => {
  it('renders heading, body and two buttons', () => {
    render(<DisclaimerPage />)
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument()
  })

  it('navigates to / on Back', async () => {
    render(<DisclaimerPage />)
    await userEvent.click(screen.getByRole('button', { name: /back/i }))
    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('navigates to /check on Continue', async () => {
    render(<DisclaimerPage />)
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(mockPush).toHaveBeenCalledWith('/check')
  })
})

// CO authors the same words for both seasons, so a key swap is invisible against
// its bundle. DC's copy differs, so these read against DC's.
describe('DisclaimerPage — season copy', () => {
  beforeAll(() => {
    i18n.addResourceBundle('en', 'disclaimer', dcDisclaimer, true, true)
  })

  afterEach(() => {
    mockFeatures = {}
  })

  it('explains the tool in the present tense while the season is enrolling', () => {
    render(<DisclaimerPage />)
    expect(screen.getByText(dcDisclaimer.body2)).toBeInTheDocument()
    expect(screen.queryByText(dcDisclaimer.closedBody2)).toBeNull()
  })

  it('says enrollment has closed once the season is over', () => {
    mockFeatures = { enrollment: { enabled: false } }
    render(<DisclaimerPage />)
    expect(screen.getByText(dcDisclaimer.closedBody2)).toBeInTheDocument()
    expect(screen.getByText(dcDisclaimer.closedBody4)).toBeInTheDocument()
  })

  // The sheet holds no closed variant of the privacy lead: it reads the same
  // either way, so the closed page has to keep using the open key.
  it('keeps the season-neutral privacy lead in both seasons', () => {
    mockFeatures = { enrollment: { enabled: false } }
    render(<DisclaimerPage />)
    expect(screen.getByText(dcDisclaimer.body3)).toBeInTheDocument()
  })
})
