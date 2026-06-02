import { act, render, screen } from '@testing-library/react'
import { useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
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
