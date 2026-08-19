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
 * Renders the standard portal chrome (header, help, footer) on every route,
 * including the outage page. The beta banner is withheld on the outage route
 * to keep the maintenance presentation minimal.
 */
export function AppShell({ children, state }: AppShellProps) {
  const pathname = usePathname()
  const isOutagePage = pathname === OUTAGE_PATH

  return (
    <div className="display-flex flex-column minh-viewport">
      {!isOutagePage && <BetaBanner />}
      <Header state={state} />
      <main
        id="main-content"
        className="flex-fill"
      >
        <OutageGuard>{children}</OutageGuard>
      </main>
      <HelpSection state={state} />
      <Footer state={state} />
    </div>
  )
}
