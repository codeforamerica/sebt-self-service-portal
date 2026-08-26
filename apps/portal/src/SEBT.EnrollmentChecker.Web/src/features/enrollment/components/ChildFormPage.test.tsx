import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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

  // Flows without a review step hand the check straight to their caller. The
  // prop's presence is what selects that behavior, so the component stays
  // unaware of which state it is running in.
  describe('direct submit (no review step)', () => {
    async function fillAndSubmit() {
      await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
      await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
      await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), 'April')
      await userEvent.type(screen.getByRole('textbox', { name: /day/i }), '12')
      await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '2015')
      await userEvent.click(screen.getByRole('button', { name: /continue|submit|check/i }))
    }

    beforeEach(() => {
      mockPush.mockClear()
      sessionStorage.clear()
    })
    afterEach(() => sessionStorage.clear())

    it('submits the newly entered child instead of routing to review', async () => {
      const onSubmitChildren = vi.fn()
      render(
        <ChildFormPage
          showSchoolField={false}
          apiBaseUrl=""
          onSubmitChildren={onSubmitChildren}
        />,
        { wrapper }
      )

      await fillAndSubmit()

      expect(onSubmitChildren).toHaveBeenCalledTimes(1)
      // The context update has not landed yet, so the child must come through
      // the payload rather than from state.
      const submitted = onSubmitChildren.mock.calls[0]![0] as Array<{ firstName: string }>
      expect(submitted).toHaveLength(1)
      expect(submitted[0]!.firstName).toBe('Jane')
      expect(mockPush).not.toHaveBeenCalledWith('/review')
    })

    // Single-child flow: a second check covers only the child just entered, so
    // households never accumulate across checks.
    it('submits only the current child when one was already checked', async () => {
      const onSubmitChildren = vi.fn()
      sessionStorage.setItem(
        'enrollmentState',
        JSON.stringify({
          children: [
            { id: 'prior', firstName: 'Alex', lastName: 'Prior', dateOfBirth: '2014-01-01' }
          ],
          editingChildId: null
        })
      )

      render(
        <ChildFormPage
          showSchoolField={false}
          apiBaseUrl=""
          onSubmitChildren={onSubmitChildren}
        />,
        { wrapper }
      )

      await fillAndSubmit()

      const submitted = onSubmitChildren.mock.calls[0]![0] as Array<{ firstName: string }>
      expect(submitted.map((c) => c.firstName)).toEqual(['Jane'])
    })

    it('routes to review when no direct-submit handler is given', async () => {
      render(<ChildFormPage showSchoolField={false} apiBaseUrl="" />, { wrapper })

      await fillAndSubmit()

      expect(mockPush).toHaveBeenCalledWith('/review')
    })
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

    it('payload key set excludes PII field names', () => {
      // Render in edit mode so a child with full PII is in EnrollmentProvider state.
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Check')
      window.digitalData!.page.set('application', 'sebt-enrollment-checker')

      render(<ChildFormPageInEditMode />, { wrapper })

      const event = window.digitalData!.event.find(e => e.eventName === 'enrollment_check_start')
      const keys = Object.keys(event!.eventData)
      // Guard against a vacuous pass on `{}`: assert the merge actually populated the bag.
      expect(keys.length).toBeGreaterThan(0)
      // Allow-list check: PII field names must not appear, even if a future
      // refactor accidentally writes them to page.* with no scope.
      const piiKeys = ['firstName', 'lastName', 'middleName', 'dateOfBirth', 'schoolName', 'schoolCode']
      for (const piiKey of piiKeys) {
        expect(keys).not.toContain(piiKey)
      }
    })

    it('filters scope-restricted fields out of the payload (e.g. user.email)', () => {
      new DataLayer('digitalData')
      window.digitalData!.page.set('name', 'Check')
      // user.set enforces 'default' scope — no analytics access.
      window.digitalData!.user.set('email', 'private@example.com')

      render(<ChildFormPage showSchoolField={false} apiBaseUrl="" />, { wrapper })

      const event = window.digitalData!.event.find(e => e.eventName === 'enrollment_check_start')
      const serialized = JSON.stringify(event!.eventData)
      expect(serialized).not.toContain('private@example.com')
    })
  })
})
