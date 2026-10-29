import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { FeatureFlagsContext } from '@/features/feature-flags'
import type { RuntimeConfig } from '@/lib/runtime-config'
import { RuntimeConfigProvider } from '@/providers'
import { PEAK_APPLY_CONFIG, PEAK_APPLY_URL, makeRuntimeConfig } from '@/test-utils/runtime-config'

import { EmptyState } from './EmptyState'

function withApplyFlag(
  children: ReactNode,
  enableApply: boolean,
  config: RuntimeConfig = PEAK_APPLY_CONFIG
) {
  return (
    <RuntimeConfigProvider config={config}>
      <FeatureFlagsContext.Provider
        value={{ flags: { enable_apply: enableApply }, isLoading: false, isError: false }}
      >
        {children}
      </FeatureFlagsContext.Provider>
    </RuntimeConfigProvider>
  )
}

describe('EmptyState', () => {
  it('renders a warning alert', () => {
    render(<EmptyState />)

    const alert = screen.getByRole('alert')
    expect(alert).toBeInTheDocument()
  })

  it('does not render an apply link when applications are open but no apply URL is configured', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    render(withApplyFlag(<EmptyState />, true, makeRuntimeConfig()))

    expect(screen.queryByRole('link')).toBeNull()
    warn.mockRestore()
  })

  it('renders an apply link pointing at the configured apply form when applications are open', () => {
    render(withApplyFlag(<EmptyState />, true))

    expect(screen.getByRole('link')).toHaveAttribute('href', `${PEAK_APPLY_URL}?language=en_US`)
  })

  it('does not render the apply link when the enable_apply flag is off', () => {
    render(withApplyFlag(<EmptyState />, false))

    expect(screen.queryByRole('link')).toBeNull()
  })

  it('does not render the apply link outside the feature-flags provider (fail closed)', () => {
    render(
      <RuntimeConfigProvider config={PEAK_APPLY_CONFIG}>
        <EmptyState />
      </RuntimeConfigProvider>
    )

    expect(screen.queryByRole('link')).toBeNull()
  })
})
