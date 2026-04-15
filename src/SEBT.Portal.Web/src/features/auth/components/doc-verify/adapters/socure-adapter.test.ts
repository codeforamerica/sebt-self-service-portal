import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { SocureDocVAdapter } from './socure-adapter'
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

// Simulate the global SDK being available (as if bundle.js loaded)
function installMockSocureGlobal() {
  window.SocureDocVSDK = {
    launch: vi.fn(),
    reset: vi.fn()
  }
}

describe('SocureDocVAdapter', () => {
  let container: HTMLDivElement

  beforeEach(() => {
    container = document.createElement('div')
    container.id = 'websdk'
    document.body.appendChild(container)
  })

  afterEach(() => {
    document.body.removeChild(container)
    delete window.SocureDocVSDK
  })

  it('reports isLoaded as false when SDK global is not available', () => {
    const adapter = new SocureDocVAdapter()
    expect(adapter.isLoaded()).toBe(false)
  })

  it('reports isLoaded as true when SDK global is available', () => {
    installMockSocureGlobal()
    const adapter = new SocureDocVAdapter()
    expect(adapter.isLoaded()).toBe(true)
  })

  it('calls SocureDocVSDK.launch with correct config when global is available', async () => {
    installMockSocureGlobal()
    const adapter = new SocureDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)

    expect(window.SocureDocVSDK!.launch).toHaveBeenCalledWith(
      'test-sdk-key',
      'test-token',
      '#websdk',
      expect.objectContaining({
        type: 'docv',
        autoOpenTabOnMobile: true,
        closeCaptureWindowOnComplete: true
      })
    )
  })

  it('is idempotent — second launch does not call SDK again', async () => {
    installMockSocureGlobal()
    const adapter = new SocureDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)
    await adapter.launch(config)

    expect(window.SocureDocVSDK!.launch).toHaveBeenCalledTimes(1)
  })

  it('calls SocureDocVSDK.reset and tears down global on reset()', () => {
    installMockSocureGlobal()
    const resetFn = window.SocureDocVSDK!.reset
    const adapter = new SocureDocVAdapter()

    adapter.reset()

    expect(resetFn).toHaveBeenCalled()
    expect(window.SocureDocVSDK).toBeUndefined()
  })

  it('can be re-launched after reset', async () => {
    installMockSocureGlobal()
    const adapter = new SocureDocVAdapter()
    const config = createTestConfig()

    await adapter.launch(config)
    adapter.reset()
    // Re-install the global since reset() tears it down for a fresh load
    installMockSocureGlobal()
    await adapter.launch(config)

    expect(window.SocureDocVSDK!.launch).toHaveBeenCalledTimes(1)
  })

  it('swallows errors from SocureDocVSDK.reset()', () => {
    window.SocureDocVSDK = {
      launch: vi.fn(),
      reset: vi.fn(() => {
        throw new Error('SDK already torn down')
      })
    }
    const adapter = new SocureDocVAdapter()

    // Should not throw
    expect(() => adapter.reset()).not.toThrow()
  })
})
