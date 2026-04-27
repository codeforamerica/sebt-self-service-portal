/**
 * AmplitudeAnalytics Component Unit Tests
 *
 * Covers the component's lifecycle contract with initAmplitudeBridge:
 * - mount: calls initAmplitudeBridge once with the provided apiKey
 * - unmount: invokes the returned teardown
 * - re-render with the same apiKey: no re-init, no teardown
 * - apiKey change: tears down the previous bridge and re-inits with the new key
 */
import { render } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const mockTeardown = vi.fn()
const mockInit = vi.fn().mockReturnValue(mockTeardown)
vi.mock('@sebt/analytics', () => ({
  initAmplitudeBridge: (apiKey: string, amp: unknown) => mockInit(apiKey, amp)
}))

// Stub the Amplitude SDK import so the test doesn't pull the real package (or
// its network/identity side effects) into the vitest environment.
vi.mock('@amplitude/analytics-browser', () => ({
  init: vi.fn(),
  track: vi.fn()
}))

import { AmplitudeAnalytics } from './AmplitudeAnalytics'

describe('AmplitudeAnalytics', () => {
  beforeEach(() => {
    mockInit.mockClear()
    mockTeardown.mockClear()
    mockInit.mockReturnValue(mockTeardown)
  })

  it('calls initAmplitudeBridge exactly once on mount with the provided apiKey', () => {
    render(<AmplitudeAnalytics apiKey="test-key" />)

    expect(mockInit).toHaveBeenCalledTimes(1)
    expect(mockInit).toHaveBeenCalledWith('test-key', expect.anything())
  })

  it('invokes the teardown function on unmount', () => {
    const { unmount } = render(<AmplitudeAnalytics apiKey="test-key" />)

    expect(mockTeardown).not.toHaveBeenCalled()
    unmount()
    expect(mockTeardown).toHaveBeenCalledTimes(1)
  })

  it('does not re-init when the component re-renders with the same apiKey', () => {
    const { rerender } = render(<AmplitudeAnalytics apiKey="test-key" />)
    expect(mockInit).toHaveBeenCalledTimes(1)

    rerender(<AmplitudeAnalytics apiKey="test-key" />)
    rerender(<AmplitudeAnalytics apiKey="test-key" />)

    expect(mockInit).toHaveBeenCalledTimes(1)
    expect(mockTeardown).not.toHaveBeenCalled()
  })

  it('tears down and re-inits when the apiKey changes', () => {
    const { rerender } = render(<AmplitudeAnalytics apiKey="key-1" />)
    expect(mockInit).toHaveBeenCalledWith('key-1', expect.anything())

    rerender(<AmplitudeAnalytics apiKey="key-2" />)

    expect(mockTeardown).toHaveBeenCalledTimes(1)
    expect(mockInit).toHaveBeenCalledTimes(2)
    expect(mockInit).toHaveBeenLastCalledWith('key-2', expect.anything())
  })
})
