import { act, render, screen } from '@testing-library/react'
import { useEffect } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { i18n } from '@sebt/design-system/client'
import { EnrollmentProvider, useEnrollment } from '@/features/enrollment/context/EnrollmentContext'
import { checkEnrollment } from '@/features/enrollment/api/checkEnrollment'
import Page from './page'

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }))

vi.mock('@/features/enrollment/api/checkEnrollment', () => ({
  checkEnrollment: vi.fn()
}))

vi.mock('@/lib/stateConfig', () => ({
  getEnrollmentConfig: () => ({ apiBaseUrl: '' })
}))

const mockedCheckEnrollment = vi.mocked(checkEnrollment)

// Seeds one child so the Submit button is enabled, then renders the page wrapper.
function PageWithChild() {
  return (
    <EnrollmentProvider>
      <Seeder />
      <Page />
    </EnrollmentProvider>
  )
}
function Seeder() {
  const { addChild } = useEnrollment()
  useEffect(() => {
    addChild({ firstName: 'Jane', lastName: 'Doe', month: '4', day: '12', year: '2015' })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  return null
}

describe('review Page — double-submit guard', () => {
  beforeEach(() => {
    sessionStorage.clear()
    mockedCheckEnrollment.mockReset()
    mockedCheckEnrollment.mockResolvedValue({ results: [] } as never)
  })

  it('fires checkEnrollment only once when Submit is double-clicked in the same tick', async () => {
    render(<PageWithChild />)
    const submit = await screen.findByRole('button', { name: /submit/i })

    // Two synchronous clicks before React can re-render reproduce the race: both
    // handlers run against the same render, so a state-only guard lets both through.
    await act(async () => {
      submit.click()
      submit.click()
    })

    expect(mockedCheckEnrollment).toHaveBeenCalledTimes(1)
  })
})

describe('review Page — submit error copy', () => {
  beforeEach(() => {
    sessionStorage.clear()
    mockedCheckEnrollment.mockReset()
  })

  afterEach(async () => {
    await act(async () => {
      await i18n.changeLanguage('en')
    })
  })

  // Renders in English (the default), finds the Submit button by its English label,
  // optionally switches language, then clicks. The banner message resolves in the
  // render path from the active language, so it always reflects the current selection.
  async function renderAndSubmit(language?: 'es') {
    render(<PageWithChild />)
    const button = await screen.findByRole('button', { name: /submit/i })
    if (language) {
      await act(async () => {
        await i18n.changeLanguage(language)
      })
    }
    await act(async () => {
      button.click()
    })
  }

  it('shows helpful copy, not the raw key, when the check fails with a server error', async () => {
    mockedCheckEnrollment.mockRejectedValue(new Error('enrollment check failed: 500'))
    await renderAndSubmit()
    expect(await screen.findByText(/system maintenance/i)).toBeInTheDocument()
    expect(screen.queryByText('submitError')).toBeNull()
  })

  it('shows the Spanish maintenance copy when the language is Spanish', async () => {
    mockedCheckEnrollment.mockRejectedValue(new Error('enrollment check failed: 503'))
    await renderAndSubmit('es')
    expect(await screen.findByText(/mantenimiento del sistema/i)).toBeInTheDocument()
  })

  it('shows helpful copy, not the raw key, on a rate-limit (429) failure', async () => {
    mockedCheckEnrollment.mockRejectedValue(
      new Error('rate limit exceeded — please wait before trying again')
    )
    await renderAndSubmit()
    expect(await screen.findByText(/too many requests/i)).toBeInTheDocument()
    expect(screen.queryByText('rateLimitError')).toBeNull()
  })

  it('re-translates the error banner immediately when the language flips, without a resubmit', async () => {
    mockedCheckEnrollment.mockRejectedValue(new Error('enrollment check failed: 502'))
    await renderAndSubmit()
    expect(await screen.findByText(/system maintenance/i)).toBeInTheDocument()

    await act(async () => {
      await i18n.changeLanguage('es')
    })

    expect(await screen.findByText(/mantenimiento del sistema/i)).toBeInTheDocument()
    expect(screen.queryByText(/system maintenance/i)).toBeNull()
    expect(mockedCheckEnrollment).toHaveBeenCalledTimes(1)
  })
})
