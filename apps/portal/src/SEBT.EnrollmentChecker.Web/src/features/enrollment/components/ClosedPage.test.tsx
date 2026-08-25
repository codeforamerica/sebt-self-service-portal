import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { EnrollmentProvider } from '../context/EnrollmentContext'
import { ClosedPage } from './ClosedPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

function renderClosedPage() {
  return render(
    <EnrollmentProvider>
      <ClosedPage />
    </EnrollmentProvider>
  )
}

// Runs against CO content, so these cover the accordion branch. The flat branch
// DC uses is covered by landingConfig.test.ts.
describe('ClosedPage', () => {
  beforeEach(() => {
    mockPush.mockClear()
    sessionStorage.clear()
  })
  afterEach(() => sessionStorage.clear())

  it('renders the closed-season heading and subtitle', () => {
    renderClosedPage()
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(/summer food benefits/i)
    expect(screen.getByText(/enrollment in summer ebt for 2026 is now closed/i)).toBeInTheDocument()
  })

  it('offers a start button per supported language using closed-variant copy', () => {
    renderClosedPage()
    expect(screen.getByRole('button', { name: /check enrollment/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /verificar inscripción/i })).toBeInTheDocument()
  })

  // The check still works after the season closes, so the buttons lead into the
  // same flow rather than a dead end.
  it('navigates into the check flow on action click', async () => {
    renderClosedPage()
    await userEvent.click(screen.getByRole('button', { name: /check enrollment/i }))
    expect(mockPush).toHaveBeenCalledWith('/disclaimer')
  })

  it('keeps the established analytics identifiers', () => {
    renderClosedPage()
    expect(screen.getByRole('button', { name: /check enrollment/i })).toHaveAttribute(
      'data-analytics-cta',
      'start_enrollment_check_cta'
    )
  })

  // Rendering the \n-delimited source as one paragraph would run items together.
  it('renders the enrollment-reason list as list items, never an empty list', () => {
    const { container } = renderClosedPage()
    const lists = container.querySelectorAll('.usa-accordion__content ul')
    expect(lists.length).toBeGreaterThan(0)
    lists.forEach((list) => {
      expect(list.querySelectorAll('li').length).toBeGreaterThan(0)
    })
  })

  it('clears persisted children on mount', () => {
    sessionStorage.setItem(
      'enrollmentState',
      JSON.stringify({
        children: [{ id: 'a', firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' }],
        editingChildId: null
      })
    )
    renderClosedPage()
    expect(sessionStorage.getItem('enrollmentState')).toBeNull()
  })
})
