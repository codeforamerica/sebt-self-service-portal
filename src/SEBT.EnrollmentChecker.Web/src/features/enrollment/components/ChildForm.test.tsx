import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ChildForm } from './ChildForm'

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

describe('ChildForm', () => {
  it('renders required fields including month dropdown and day/year inputs', () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    expect(screen.getByRole('textbox', { name: /first name/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /last name/i })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: /month/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /day/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /year/i })).toBeInTheDocument()
  })

  it('renders hint text for first name and last name', () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    // Both first name and last name show this hint, so there are multiple matches
    const hints = screen.getAllByText(/legally as it appears/i)
    expect(hints.length).toBeGreaterThanOrEqual(1)
  })

  it('does not render school field when showSchoolField is false', () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    // SchoolSelect renders a combobox when enabled. The month dropdown is the only combobox otherwise.
    const comboboxes = screen.getAllByRole('combobox')
    expect(comboboxes).toHaveLength(1) // only the month dropdown
  })

  it('shows validation error on submit when firstName is empty', async () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )

    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), 'April')
    await userEvent.type(screen.getByRole('textbox', { name: /day/i }), '12')
    await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '2020')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    // TODO update once error message copy is added
    expect(await screen.findByText(/Enter child/i)).toBeInTheDocument()
  })

  it('shows validation error on submit when lastName contains a number', async () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe1')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), 'April')
    await userEvent.type(screen.getByRole('textbox', { name: /day/i }), '12')
    await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '2020')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
     // TODO update once error message copy is added
    expect(await screen.findByText(/Enter child/i)).toBeInTheDocument()
  })

  it('shows validation error on submit when day is empty', async () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), 'April')
    await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '2020')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(await screen.findByText(/Provide a day/i)).toBeInTheDocument()
  })

  it('shows validation error on submit when day is invalid', async () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), 'April')
    await userEvent.type(screen.getByRole('textbox', { name: /day/i }), '42')
    await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '2020')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(await screen.findByText(/Provide a day/i)).toBeInTheDocument()
  })

  it('shows validation error on submit when year is invalid', async () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )

    await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), 'April')
    await userEvent.type(screen.getByRole('textbox', { name: /day/i }), '12')
    await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '1888')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(await screen.findByText(/Provide a year/i)).toBeInTheDocument()
  })

  it('calls onSubmit with valid values including separate date fields', async () => {
    const onSubmit = vi.fn()
    render(
      <ChildForm
        onSubmit={onSubmit}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: /month/i }), 'April')
    await userEvent.type(screen.getByRole('textbox', { name: /day/i }), '12')
    await userEvent.type(screen.getByRole('textbox', { name: /year/i }), '2015')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        firstName: 'Jane',
        lastName: 'Doe',
        month: '4',
        day: '12',
        year: '2015'
      })
    )
  })

  it('uses Back label instead of Cancel for the cancel button', () => {
    render(
      <ChildForm
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
        showSchoolField={false}
        apiBaseUrl=""
      />,
      { wrapper }
    )
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument()
  })
})
