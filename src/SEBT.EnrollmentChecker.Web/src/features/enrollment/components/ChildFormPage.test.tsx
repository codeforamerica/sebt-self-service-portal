import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { useEffect } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
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
})
