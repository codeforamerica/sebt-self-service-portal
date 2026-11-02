// @vitest-environment node
// Node, not jsdom: with a window present, createEnv treats the run as client-side and skips the server schema.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

async function register(): Promise<void> {
  const instrumentation = await import('./instrumentation')
  return instrumentation.register()
}

describe('register', () => {
  beforeEach(() => {
    // Each case re-evaluates env.ts against its own stubbed environment.
    vi.resetModules()
    // vitest.config.ts skips env validation for every other suite; this one exercises it.
    vi.stubEnv('SKIP_ENV_VALIDATION', '')
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.unstubAllEnvs()
    vi.restoreAllMocks()
  })

  it('starts when the application link variables are valid', async () => {
    vi.stubEnv('APPLICATION_URL', 'https://example.gov/apply')
    vi.stubEnv('APPLICATION_URL_PEAK_PARAMS', 'true')

    await expect(register()).resolves.toBeUndefined()
  })

  it('refuses to start, naming the variable, when the application URL is malformed', async () => {
    vi.stubEnv('APPLICATION_URL', 'not a url')

    await expect(register()).rejects.toThrow('Invalid environment variables')
    expect(console.error).toHaveBeenCalledWith(
      expect.any(String),
      expect.arrayContaining([expect.objectContaining({ path: ['APPLICATION_URL'] })])
    )
  })

  it('accepts the PEAK params toggle in any case', async () => {
    vi.stubEnv('APPLICATION_URL_PEAK_PARAMS', 'TRUE')

    await expect(register()).resolves.toBeUndefined()
  })

  it('refuses to start when the PEAK params toggle is neither true nor false', async () => {
    vi.stubEnv('APPLICATION_URL_PEAK_PARAMS', 'yes')

    await expect(register()).rejects.toThrow('Invalid environment variables')
    expect(console.error).toHaveBeenCalledWith(
      expect.any(String),
      expect.arrayContaining([expect.objectContaining({ path: ['APPLICATION_URL_PEAK_PARAMS'] })])
    )
  })
})
