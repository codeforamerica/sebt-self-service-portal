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

// Must mirror useCheckerFeatures' queryKey (apiBaseUrl is '' via the mock above).
const QUERY_KEY = ['checker-features', '']

// Must mirror the hook's polling cadence so the staleness tests advance real cycles.
const REFETCH_INTERVAL_MS = 60_000

const EN_COPY = 'The enrollment checker may be temporarily unavailable due to system maintenance.'
const ES_COPY = 'El verificador de inscripción puede no estar disponible temporalmente.'

function renderBanner() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const view = render(<MaintenanceBanner />, {
    wrapper: ({ children }) => <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  })
  return { ...view, qc }
}

function mockFeatures(enabled: boolean, message: Record<string, string> = { en: EN_COPY, es: ES_COPY }) {
  const calls = { count: 0 }
  server.use(
    http.get('/api/enrollment/features', () => {
      calls.count += 1
      return HttpResponse.json({ maintenanceBanner: { enabled, message } })
    })
  )
  return calls
}

function mockFeaturesFailure() {
  server.use(
    http.get('/api/enrollment/features', () => new HttpResponse(null, { status: 500 }))
  )
}

// Asserting against the pending render proves nothing (every component with a
// loading state renders empty there); wait for the query to actually settle first.
async function waitForSettled(qc: QueryClient, status: 'success' | 'error') {
  await waitFor(() => expect(qc.getQueryState(QUERY_KEY)?.status).toBe(status))
}

async function advanceOnePollCycle() {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(REFETCH_INTERVAL_MS)
  })
}

function warnText(warn: { mock: { calls: unknown[][] } }) {
  return warn.mock.calls.map((call) => String(call[0])).join('\n')
}

describe('MaintenanceBanner', () => {
  afterEach(async () => {
    vi.useRealTimers()
    vi.restoreAllMocks()
    await act(async () => {
      await i18n.changeLanguage('en')
    })
  })

  it('renders nothing when the flag is off', async () => {
    mockFeatures(false)
    const { container, qc } = renderBanner()
    await waitForSettled(qc, 'success')
    expect(container).toBeEmptyDOMElement()
  })

  it('renders the configured message as an alert when the flag is on', async () => {
    mockFeatures(true)
    renderBanner()
    // role="alert" (assertive live region) is the intended semantics for this
    // banner; screen readers announce it immediately when it mounts.
    expect(await screen.findByRole('alert')).toHaveTextContent(EN_COPY)
  })

  it('re-resolves the message when the language changes, without a refetch', async () => {
    const calls = mockFeatures(true)
    renderBanner()
    expect(await screen.findByText(EN_COPY)).toBeInTheDocument()

    await act(async () => {
      await i18n.changeLanguage('es')
    })

    expect(await screen.findByText(ES_COPY)).toBeInTheDocument()
    expect(screen.queryByText(EN_COPY)).toBeNull()
    expect(calls.count).toBe(1)
  })

  it('renders nothing and warns when the initial features request fails', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    mockFeaturesFailure()
    const { container, qc } = renderBanner()
    await waitForSettled(qc, 'error')
    expect(container).toBeEmptyDOMElement()
    expect(warnText(warn)).toMatch(/hiding banner/i)
  })

  it('keeps showing the last-known banner state when a poll fails within the staleness tolerance', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    mockFeatures(true)
    const { qc } = renderBanner()
    expect(await screen.findByText(EN_COPY)).toBeInTheDocument()

    mockFeaturesFailure()
    await advanceOnePollCycle()
    await waitForSettled(qc, 'error')

    expect(screen.getByText(EN_COPY)).toBeInTheDocument()
    expect(warnText(warn)).toMatch(/last-known/i)
    expect(warnText(warn)).not.toMatch(/hiding/i)
  })

  it('hides the banner once poll failures outlast the staleness tolerance', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    mockFeatures(true)
    const { container, qc } = renderBanner()
    expect(await screen.findByText(EN_COPY)).toBeInTheDocument()

    mockFeaturesFailure()
    // Tolerance is five missed polls; the sixth failed poll crosses it.
    for (let cycle = 0; cycle < 6; cycle += 1) {
      await advanceOnePollCycle()
    }
    await waitForSettled(qc, 'error')

    await waitFor(() => expect(container).toBeEmptyDOMElement())
  })

  it('polls the features endpoint again after the refetch interval', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const calls = mockFeatures(true)
    renderBanner()
    expect(await screen.findByText(EN_COPY)).toBeInTheDocument()
    expect(calls.count).toBe(1)

    await advanceOnePollCycle()
    await waitFor(() => expect(calls.count).toBe(2))
  })
})
