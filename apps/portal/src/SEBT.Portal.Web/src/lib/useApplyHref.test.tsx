import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '@/features/feature-flags'

import { useApplyHref } from './useApplyHref'

let mockState = 'dc'
vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return { ...actual, getState: () => mockState }
})

afterEach(() => {
  mockState = 'dc'
})

function createWrapper(contextValue?: FeatureFlagsContextValue) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  })

  return function Wrapper({ children }: { children: ReactNode }) {
    if (contextValue) {
      return (
        <QueryClientProvider client={queryClient}>
          <FeatureFlagsContext.Provider value={contextValue}>
            {children}
          </FeatureFlagsContext.Provider>
        </QueryClientProvider>
      )
    }
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

function flagsContext(flags: Record<string, boolean>): FeatureFlagsContextValue {
  return { flags, isLoading: false, isError: false }
}

describe('useApplyHref', () => {
  it('returns the CO PEAK URL when applications are open', () => {
    mockState = 'co'

    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: true }))
    })

    expect(result.current).toBe(
      'https://peak.my.site.com/SEBT/s/apply-for-sebt-starting-page?language=en_US'
    )
  })

  it('returns null for CO when the enable_apply flag is off', () => {
    mockState = 'co'

    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: false }))
    })

    expect(result.current).toBeNull()
  })

  it('returns null for CO when the enable_apply flag is absent (fail closed)', () => {
    mockState = 'co'

    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({}))
    })

    expect(result.current).toBeNull()
  })

  it('returns null for CO outside the feature-flags provider (fail closed)', () => {
    mockState = 'co'

    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper()
    })

    expect(result.current).toBeNull()
  })

  it('returns null for DC even when the flag is on (no DC apply destination)', () => {
    mockState = 'dc'

    const { result } = renderHook(() => useApplyHref(), {
      wrapper: createWrapper(flagsContext({ enable_apply: true }))
    })

    expect(result.current).toBeNull()
  })
})
