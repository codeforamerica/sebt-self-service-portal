'use client'

import { createContext, useContext } from 'react'

import type { RuntimeConfig } from '@/lib/runtime-config'

import type { RuntimeConfigProviderProps } from './types'

/**
 * Carries the server-resolved browser config to client components.
 *
 * The value is read once per request in the root layout and serialized into the
 * RSC payload, so client components see request-time values without a fetch and
 * without a pre-config render — the alternative (an API round-trip) would leave
 * analytics uninitialized and key-dependent UI flickering on first paint.
 */
const RuntimeConfigContext = createContext<RuntimeConfig | null>(null)

export function RuntimeConfigProvider({ config, children }: RuntimeConfigProviderProps) {
  return <RuntimeConfigContext.Provider value={config}>{children}</RuntimeConfigContext.Provider>
}

/**
 * Every vendor integration is optional, so "no config" is a valid state rather
 * than an error: an absent key already means "this integration is off".
 * Returning it outside the provider matches useFeatureFlag(), and keeps a
 * component renderable in isolation without a provider wrapper.
 */
const NO_CONFIG: RuntimeConfig = { mockSocure: false, debugRepeatOidcStepUp: false }

/** Reads browser-facing config in a client component. */
export function useRuntimeConfig(): RuntimeConfig {
  return useContext(RuntimeConfigContext) ?? NO_CONFIG
}
