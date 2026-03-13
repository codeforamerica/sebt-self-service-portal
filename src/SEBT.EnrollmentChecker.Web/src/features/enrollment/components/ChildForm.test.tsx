import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'
import { ChildForm } from './ChildForm'

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

describe('ChildForm', () => {
  it('renders required fields', () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    // Use getByRole to avoid "multiple elements found" due to aria-labelledby on the InputField wrapper
    expect(screen.getByRole('textbox', { name: /first name/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /last name/i })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: /birthdate/i })).toBeInTheDocument()
  })

  it('does not render school field when showSchoolField is false', () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  })

  it('shows validation error on submit when firstName is empty', async () => {
    render(<ChildForm onSubmit={vi.fn()} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(await screen.findByText(/first name is required/i)).toBeInTheDocument()
  })

  it('calls onSubmit with valid values', async () => {
    const onSubmit = vi.fn()
    render(<ChildForm onSubmit={onSubmit} showSchoolField={false} apiBaseUrl="" />, { wrapper })
    await userEvent.type(screen.getByRole('textbox', { name: /first name/i }), 'Jane')
    await userEvent.type(screen.getByRole('textbox', { name: /last name/i }), 'Doe')
    await userEvent.type(screen.getByRole('textbox', { name: /birthdate/i }), '2015-04-12')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Jane', lastName: 'Doe', dateOfBirth: '2015-04-12' })
    )
  })
})
