'use client'

// @see https://tanstack.com/query/latest/docs/framework/react/guides/advanced-ssr

import { isServer, QueryClient, QueryClientProvider } from '@tanstack/react-query'

import type { QueryProviderProps } from './types'

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Data is considered fresh for 1 minute
        staleTime: 60 * 1000,
        // Cache is retained for 5 minutes after component unmounts
        gcTime: 5 * 60 * 1000,
        // Retry failed queries once
        retry: 1,
        // Don't refetch when window regains focus (reduces unnecessary requests)
        refetchOnWindowFocus: false
      },
      mutations: {
        // Don't retry mutations (side effects should be explicit)
        retry: 0
      }
    }
  })
}

let browserQueryClient: QueryClient | undefined

function getQueryClient() {
  if (isServer) {
    return makeQueryClient()
  }
  if (!browserQueryClient) {
    browserQueryClient = makeQueryClient()
  }
  return browserQueryClient
}

export function QueryProvider({ children }: QueryProviderProps) {
  const queryClient = getQueryClient()

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}
