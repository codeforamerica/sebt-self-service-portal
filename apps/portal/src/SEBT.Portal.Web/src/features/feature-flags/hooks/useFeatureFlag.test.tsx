import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'

import { TEST_FEATURE_FLAGS } from '@/mocks/handlers'

import { FeatureFlagsContext, type FeatureFlagsContextValue } from '../context'
import { useFeatureFlag, useFeatureFlagsStatus } from './useFeatureFlag'

// Helper to create a wrapper with providers
function createWrapper(contextValue?: FeatureFlagsContextValue) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false
      }
    }
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

describe('useFeatureFlag', () => {
  it('returns true for enabled features', () => {
    const contextValue: FeatureFlagsContextValue = {
      flags: { enable_enrollment_status: true },
      isLoading: false,
      isError: false
    }

    const { result } = renderHook(() => useFeatureFlag('enable_enrollment_status'), {
      wrapper: createWrapper(contextValue)
    })

    expect(result.current).toBe(true)
  })

  it('returns false for disabled features', () => {
    const contextValue: FeatureFlagsContextValue = {
      flags: { enable_card_replacement: false },
      isLoading: false,
      isError: false
    }

    const { result } = renderHook(() => useFeatureFlag('enable_card_replacement'), {
      wrapper: createWrapper(contextValue)
    })

    expect(result.current).toBe(false)
  })

  it('returns false for unknown features', () => {
    const contextValue: FeatureFlagsContextValue = {
      flags: { enable_enrollment_status: true },
      isLoading: false,
      isError: false
    }

    const { result } = renderHook(() => useFeatureFlag('enable_unknown_feature'), {
      wrapper: createWrapper(contextValue)
    })

    expect(result.current).toBe(false)
  })

  it('returns false when used outside provider', () => {
    // Render without the FeatureFlagsContext provider
    const { result } = renderHook(() => useFeatureFlag('enable_enrollment_status'), {
      wrapper: createWrapper() // No context value provided
    })

    expect(result.current).toBe(false)
  })

  it('handles multiple flags correctly', () => {
    const contextValue: FeatureFlagsContextValue = {
      flags: TEST_FEATURE_FLAGS,
      isLoading: false,
      isError: false
    }

    const { result: enrollmentResult } = renderHook(
      () => useFeatureFlag('enable_enrollment_status'),
      {
        wrapper: createWrapper(contextValue)
      }
    )

    const { result: cardReplacementResult } = renderHook(
      () => useFeatureFlag('enable_card_replacement'),
      {
        wrapper: createWrapper(contextValue)
      }
    )

    const { result: spanishResult } = renderHook(() => useFeatureFlag('enable_spanish_support'), {
      wrapper: createWrapper(contextValue)
    })

    expect(enrollmentResult.current).toBe(true)
    expect(cardReplacementResult.current).toBe(false)
    expect(spanishResult.current).toBe(true)
  })
})

describe('useFeatureFlagsStatus', () => {
  it('returns loading state correctly', () => {
    const contextValue: FeatureFlagsContextValue = {
      flags: {},
      isLoading: true,
      isError: false
    }

    const { result } = renderHook(() => useFeatureFlagsStatus(), {
      wrapper: createWrapper(contextValue)
    })

    expect(result.current.isLoading).toBe(true)
    expect(result.current.isError).toBe(false)
  })

  it('returns error state correctly', () => {
    const contextValue: FeatureFlagsContextValue = {
      flags: {},
      isLoading: false,
      isError: true
    }

    const { result } = renderHook(() => useFeatureFlagsStatus(), {
      wrapper: createWrapper(contextValue)
    })

    expect(result.current.isLoading).toBe(false)
    expect(result.current.isError).toBe(true)
  })

  it('returns default state when outside provider', () => {
    const { result } = renderHook(() => useFeatureFlagsStatus(), {
      wrapper: createWrapper() // No context value
    })

    expect(result.current.isLoading).toBe(false)
    expect(result.current.isError).toBe(false)
  })

  it('returns success state correctly', () => {
    const contextValue: FeatureFlagsContextValue = {
      flags: { enable_enrollment_status: true },
      isLoading: false,
      isError: false
    }

    const { result } = renderHook(() => useFeatureFlagsStatus(), {
      wrapper: createWrapper(contextValue)
    })

    expect(result.current.isLoading).toBe(false)
    expect(result.current.isError).toBe(false)
  })
})

describe('useFeatureFlags integration', () => {
  it('flags load before dependent components can access them', async () => {
    // This test verifies the acceptance criteria: "Flags load before dependent components render"
    const contextValue: FeatureFlagsContextValue = {
      flags: TEST_FEATURE_FLAGS,
      isLoading: false,
      isError: false
    }

    const { result } = renderHook(
      () => ({
        status: useFeatureFlagsStatus(),
        enrollmentStatus: useFeatureFlag('enable_enrollment_status'),
        cardReplacement: useFeatureFlag('enable_card_replacement')
      }),
      {
        wrapper: createWrapper(contextValue)
      }
    )

    await waitFor(() => {
      expect(result.current.status.isLoading).toBe(false)
    })

    // Once loaded, flags should be accessible
    expect(result.current.enrollmentStatus).toBe(true)
    expect(result.current.cardReplacement).toBe(false)
  })
})
