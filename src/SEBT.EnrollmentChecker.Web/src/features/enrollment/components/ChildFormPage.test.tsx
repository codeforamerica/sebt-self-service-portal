import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useEffect } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { DataLayer } from '@sebt/analytics'
import { EnrollmentProvider, useEnrollment } from '../context/EnrollmentContext'
import { ChildFormPage } from './ChildFormPage'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }))

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>
    <EnrollmentProvider>{children}</EnrollmentProvider>
  </QueryClientProvider>
)

// Renders ChildFormPage only after a child has been seeded and set for editing,
// so that ChildForm receives initialValues from the start (useState captures them at mount).
function ChildFormPageInEditMode() {
  const { addChild, setEditingChildId, state } = useEnrollment()

  // Seed one child on mount
  useEffect(() => {
    addChild({ firstName: 'Jane', lastName: 'Doe', month: '4', day: '12', year: '2015' })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Once the child exists, set it as the editing target
  useEffect(() => {
    if (state.children.length > 0 && state.children[0]) {
      setEditingChildId(state.children[0].id)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.children.length])

  // Delay mounting ChildFormPage until editingChildId is set, so ChildForm's
  // useState captures the correct initialValues on its first render.
  if (!state.editingChildId) return null

  return <ChildFormPage showSchoolField={false} apiBaseUrl="" />
}

describe('ChildFormPage', () => {
  it('renders in add mode by default', () => {
    render(<ChildFormPage showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
  })

  it('shows back navigation when no children yet', () => {
    render(<ChildFormPage showSchoolField={false} apiBaseUrl="" />, { wrapper })
    // The page renders back navigation (both an unstyled top-level back button
    // and a back button inside the form's button group)
    const backButtons = screen.getAllByRole('button', { name: /back/i })
    expect(backButtons.length).toBeGreaterThan(0)
  })

  it('renders edit heading when a child is being edited', async () => {
    render(<ChildFormPageInEditMode />, { wrapper })
    // Wait for edit heading to appear (after effects run)
    expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument()
    // In edit mode, the form should be pre-populated with the child's firstName
    expect(await screen.findByDisplayValue('Jane')).toBeInTheDocument()
  })

  describe('analytics — DC-178', () => {
    beforeEach(() => {
      delete (window as unknown as Record<string, unknown>).digitalData
    })
    afterEach(() => {
      delete (window as unknown as Record<string, unknown>).digitalData
    })

    it('fires enrollment_check_start with name + application from the data layer', () => {
      new DataLayer('digitalData')
      // page.name + page.application are normally set by DataLayerProvider;
      // seeded directly here to isolate the trackEvent merge contract.
      window.digitalData!.page.set('name', 'Check')
      window.digitalData!.page.set('application', 'sebt-enrollment-checker')

      render(<ChildFormPage showSchoolField={false} apiBaseUrl="" />, { wrapper })

      const event = window.digitalData!.event.find(e => e.eventName === 'enrollment_check_start')
      expect(event).toBeDefined()
      expect(event!.eventData).toEqual(expect.objectContaining({
        name: 'Check',
        application: 'sebt-enrollment-checker'
      }))
    })

    it('does not include child PII in the enrollment_check_start payload', () => {
      new DataLayer('digitalData')

      // Render in edit mode so a child with full PII is in EnrollmentProvider state.
      render(<ChildFormPageInEditMode />, { wrapper })

      const event = window.digitalData!.event.find(e => e.eventName === 'enrollment_check_start')
      expect(event).toBeDefined()
      const serialized = JSON.stringify(event!.eventData)
      expect(serialized).not.toContain('Jane')
      expect(serialized).not.toContain('Doe')
      expect(serialized).not.toContain('2015')
    })
  })
})
