import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server } from '../../../mocks/server'
import { SchoolSelect } from './SchoolSelect'

const qcWrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    {children}
  </QueryClientProvider>
)

describe('SchoolSelect', () => {
  it('renders nothing when not enabled', () => {
    const { container } = render(
      <SchoolSelect enabled={false} apiBaseUrl="" value="" onChange={vi.fn()} />,
      { wrapper: qcWrapper }
    )
    expect(container.firstChild).toBeNull()
  })

  it('renders a select when enabled and schools load', async () => {
    server.use(
      http.get('/api/enrollment/schools', () =>
        HttpResponse.json([{ name: 'Elm School', code: 'ELM' }])
      )
    )
    render(
      <SchoolSelect enabled={true} apiBaseUrl="" value="" onChange={vi.fn()} />,
      { wrapper: qcWrapper }
    )
    await waitFor(() =>
      expect(screen.getByRole('combobox')).toBeInTheDocument()
    )
    expect(screen.getByText('Elm School')).toBeInTheDocument()
  })

  it('calls onChange with code and name when a school is selected', async () => {
    server.use(
      http.get('/api/enrollment/schools', () =>
        HttpResponse.json([
          { name: 'Elm School', code: 'ELM' },
          { name: 'Oak Academy', code: 'OAK' }
        ])
      )
    )
    const onChange = vi.fn()
    render(
      <SchoolSelect enabled={true} apiBaseUrl="" value="" onChange={onChange} />,
      { wrapper: qcWrapper }
    )
    await waitFor(() => expect(screen.getByRole('combobox')).toBeInTheDocument())

    await userEvent.selectOptions(screen.getByRole('combobox'), 'ELM')
    expect(onChange).toHaveBeenCalledWith('ELM', 'Elm School')
  })
})
