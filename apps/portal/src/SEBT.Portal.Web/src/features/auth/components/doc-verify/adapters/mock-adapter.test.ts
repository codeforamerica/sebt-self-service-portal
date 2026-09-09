import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { MockDocVAdapter } from './mock-adapter'
import type { DocVAdapterConfig } from './types'

function createTestConfig(overrides: Partial<DocVAdapterConfig> = {}): DocVAdapterConfig {
  return {
    sdkKey: 'test-sdk-key',
    token: 'test-token',
    containerId: 'websdk',
    onSuccess: vi.fn(),
    onError: vi.fn(),
    onProgress: vi.fn(),
    ...overrides
  }
}

describe('MockDocVAdapter', () => {
  let container: HTMLDivElement

  beforeEach(() => {
    vi.useFakeTimers()
    container = document.createElement('div')
    container.id = 'websdk'
    document.body.appendChild(container)
  })

  afterEach(() => {
    vi.useRealTimers()
    document.body.removeChild(container)
  })

  it('is always loaded', () => {
    const adapter = new MockDocVAdapter()
    expect(adapter.isLoaded()).toBe(true)
  })

  it('renders mock capture UI into the container on launch', async () => {
    const adapter = new MockDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)

    expect(container.textContent).toContain('Mock Document Capture')
  })

  it('fires onProgress immediately on launch', async () => {
    const adapter = new MockDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)

    expect(config.onProgress).toHaveBeenCalledWith({ type: 'documentDetected' })
  })

  it('fires onSuccess after the delay', async () => {
    const adapter = new MockDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)

    expect(config.onSuccess).not.toHaveBeenCalled()

    vi.advanceTimersByTime(1500)

    expect(config.onSuccess).toHaveBeenCalledWith({
      documentType: 'mock-license',
      status: 'captured'
    })
  })

  it('is idempotent — second launch is a no-op', async () => {
    const adapter = new MockDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)
    await adapter.launch(config)

    // onProgress should only fire once
    expect(config.onProgress).toHaveBeenCalledTimes(1)
  })

  it('cancels the pending timeout on reset', async () => {
    const adapter = new MockDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)
    adapter.reset()

    vi.advanceTimersByTime(2000)

    expect(config.onSuccess).not.toHaveBeenCalled()
  })

  it('can be re-launched after reset', async () => {
    const adapter = new MockDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)
    adapter.reset()
    await adapter.launch(config)

    // onProgress fires once per launch
    expect(config.onProgress).toHaveBeenCalledTimes(2)
  })
})
