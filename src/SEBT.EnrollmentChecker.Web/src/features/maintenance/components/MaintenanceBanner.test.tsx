import { i18n } from '@sebt/design-system/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { server } from '../../../mocks/server'
import { MaintenanceBanner } from './MaintenanceBanner'

vi.mock('@/lib/stateConfig', () => ({
  getEnrollmentConfig: () => ({ apiBaseUrl: '' })
}))

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

const EN_COPY = 'The enrollment checker may be temporarily unavailable due to system maintenance.'
const ES_COPY = 'El verificador de inscripción puede no estar disponible temporalmente.'

function mockFeatures(enabled: boolean, message: Record<string, string> = { en: EN_COPY, es: ES_COPY }) {
  server.use(
    http.get('/api/enrollment/features', () =>
      HttpResponse.json({ maintenanceBanner: { enabled, message } })
    )
  )
}

describe('MaintenanceBanner', () => {
  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage('en')
    })
  })

  it('renders nothing when the flag is off', async () => {
    mockFeatures(false)
    const { container } = render(<MaintenanceBanner />, { wrapper })
    await waitFor(() => expect(container).toBeEmptyDOMElement())
  })

  it('renders the configured message when the flag is on', async () => {
    mockFeatures(true)
    render(<MaintenanceBanner />, { wrapper })
    expect(await screen.findByText(EN_COPY)).toBeInTheDocument()
  })

  it('re-resolves the message when the language changes, without a refetch', async () => {
    mockFeatures(true)
    render(<MaintenanceBanner />, { wrapper })
    expect(await screen.findByText(EN_COPY)).toBeInTheDocument()

    await act(async () => {
      await i18n.changeLanguage('es')
    })

    expect(await screen.findByText(ES_COPY)).toBeInTheDocument()
    expect(screen.queryByText(EN_COPY)).toBeNull()
  })

  it('renders nothing when the features request fails', async () => {
    server.use(
      http.get('/api/enrollment/features', () => new HttpResponse(null, { status: 500 }))
    )
    const { container } = render(<MaintenanceBanner />, { wrapper })
    await waitFor(() => expect(container).toBeEmptyDOMElement())
  })
})
