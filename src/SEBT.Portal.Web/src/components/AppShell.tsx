'use client'

import { BetaBanner } from '@/components/BetaBanner'
import { OUTAGE_PATH, OutageGuard } from '@/components/OutageGuard'
import type { StateCode } from '@sebt/design-system'
import { Footer, Header, HelpSection } from '@sebt/design-system/client'
import { usePathname } from 'next/navigation'
import type { ReactNode } from 'react'

interface AppShellProps {
  children: ReactNode
  state: StateCode
}

/**
 * Renders the standard portal chrome (header, help, footer) for normal routes.
 * The outage page uses a minimal full-viewport layout without site navigation.
 */
export function AppShell({ children, state }: AppShellProps) {
  const pathname = usePathname()
  const isOutagePage = pathname === OUTAGE_PATH

  if (isOutagePage) {
    return (
      <main
        id="main-content"
        className="minh-viewport bg-base-lightest"
      >
        <OutageGuard>{children}</OutageGuard>
      </main>
    )
  }

  return (
    <>
      <BetaBanner />
      <Header state={state} />
      <main id="main-content">
        <OutageGuard>{children}</OutageGuard>
      </main>
      <HelpSection state={state} />
      <Footer state={state} />
    </>
  )
}
