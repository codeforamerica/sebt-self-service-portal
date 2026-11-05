import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'
import type { RuntimeConfig } from '@/lib/runtime-config'
import { RuntimeConfigProvider } from '@/providers'
import { PEAK_APPLY_CONFIG, PEAK_APPLY_URL, makeRuntimeConfig } from '@/test-utils/runtime-config'

import { useApplyHref } from './useApplyHref'

function createWrapper(contextValue?: FeatureFlagsContextValue, config?: RuntimeConfig) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  })

  return function Wrapper({ children }: { children: ReactNode }) {
    const withFlags = contextValue ? (
      <FeatureFlagsContext.Provider value={contextValue}>{children}</FeatureFlagsContext.Provider>
    ) : (
      children
    )
    const withConfig = config ? (
      <RuntimeConfigProvider config={config}>{withFlags}</RuntimeConfigProvider>
    ) : (
      withFlags
    )
    return <QueryClientProvider client={queryClient}>{withConfig}</QueryClientProvider>
  }
}

function flagsContext(flags: Record<string, boolean>): FeatureFlagsContextValue {
  return { flags, isLoading: false, isError: false }
}

describe('useApplyHref', () => {
  let warnSpy: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})
  })

  afterEach(() => {
    warnSpy.mockRestore()
  })

  it('returns the configured URL with the PEAK language param when applications are open', () => {
    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: true }), PEAK_APPLY_CONFIG)
    })

    expect(result.current).toBe(`${PEAK_APPLY_URL}?language=en_US`)
    expect(warnSpy).not.toHaveBeenCalled()
  })

  it('returns the configured URL unchanged when PEAK params are off', () => {
    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(
        flagsContext({ enable_apply: true }),
        makeRuntimeConfig({ applicationUrl: 'https://example.gov/apply' })
      )
    })

    expect(result.current).toBe('https://example.gov/apply')
  })

  it('returns null without warning when the enable_apply flag is off', () => {
    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: false }), PEAK_APPLY_CONFIG)
    })

    expect(result.current).toBeNull()
    expect(warnSpy).not.toHaveBeenCalled()
  })

  it('returns null without warning when the flag is off and no URL is configured', () => {
    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: false }), makeRuntimeConfig())
    })

    expect(result.current).toBeNull()
    expect(warnSpy).not.toHaveBeenCalled()
  })

  it('returns null when the enable_apply flag is absent (fail closed)', () => {
    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({}), PEAK_APPLY_CONFIG)
    })

    expect(result.current).toBeNull()
  })

  it('returns null outside the feature-flags provider (fail closed)', () => {
    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(undefined, PEAK_APPLY_CONFIG)
    })

    expect(result.current).toBeNull()
  })

  it('returns null and warns once when the flag is on but no URL is configured', () => {
    const { result, rerender } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: true }), makeRuntimeConfig())
    })
    rerender()

    expect(result.current).toBeNull()
    expect(warnSpy).toHaveBeenCalledTimes(1)
  })

  it('returns null and warns when the flag is on outside the runtime-config provider', () => {
    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: true }))
    })

    expect(result.current).toBeNull()
    expect(warnSpy).toHaveBeenCalledTimes(1)
  })
})
