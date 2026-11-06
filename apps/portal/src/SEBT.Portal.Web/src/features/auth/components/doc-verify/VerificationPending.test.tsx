import { PageFocusProvider, PageHeading } from '@sebt/design-system'
import { act, fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { VerificationPending } from './VerificationPending'

vi.mock('next/navigation', () => ({
  usePathname: () => '/login/id-proofing/doc-verify'
}))

const mockStatus = {
  data: undefined as { status?: string } | undefined,
  error: null,
  refetch: vi.fn()
}
vi.mock('../../api', () => ({
  useVerificationStatus: () => mockStatus
}))

/** Mounts the previous screen first so the pending screen counts as a navigation. */
function AfterInterstitial() {
  const [pending, setPending] = useState(false)
  return (
    <PageFocusProvider>
      {pending ? (
        <VerificationPending
          challengeId="challenge-1"
          onVerified={() => {}}
          onRejected={() => {}}
          onResubmit={() => {}}
        />
      ) : (
        <PageHeading id="doc-verify-title">Verify your identity</PageHeading>
      )}
      <button
        type="button"
        onClick={() => setPending(true)}
      >
        continue
      </button>
    </PageFocusProvider>
  )
}

describe('VerificationPending', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('titles the pending screen with a page heading that takes focus after the interstitial', () => {
    render(<AfterInterstitial />)

    fireEvent.click(screen.getByRole('button', { name: 'continue' }))

    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading).toHaveFocus()
    expect(document.title.startsWith(`${heading.textContent?.trim()} | `)).toBe(true)
  })

  it('moves focus to the still-checking heading when the wait passes the threshold', () => {
    render(<AfterInterstitial />)
    fireEvent.click(screen.getByRole('button', { name: 'continue' }))
    const pendingText = screen.getByRole('heading', { level: 1 }).textContent

    act(() => {
      vi.advanceTimersByTime(15_000)
    })

    const heading = screen.getByRole('heading', { level: 1 })
    expect(heading.textContent).not.toBe(pendingText)
    expect(heading).toHaveFocus()
  })
})
