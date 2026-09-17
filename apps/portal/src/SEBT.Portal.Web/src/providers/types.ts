import type { ReactNode } from 'react'

import type { RuntimeConfig } from '@/lib/runtime-config'

export interface I18nProviderProps {
  children: ReactNode
}

export interface QueryProviderProps {
  children: ReactNode
}

export interface FeatureFlagsProviderProps {
  children: ReactNode
}

export interface RuntimeConfigProviderProps {
  /** Resolved on the server per request; see lib/runtime-config.ts. */
  config: RuntimeConfig
  children: ReactNode
}
