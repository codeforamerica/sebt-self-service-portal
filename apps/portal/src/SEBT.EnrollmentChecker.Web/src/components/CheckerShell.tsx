'use client'

import { OUTAGE_PATH, OutageGuard } from '@/components/OutageGuard'
import { MaintenanceBanner } from '@/features/maintenance'
import { Footer } from '@sebt/design-system/src/components/layout/Footer'
import { Header } from '@sebt/design-system/src/components/layout/Header'
import { HelpSection } from '@sebt/design-system/src/components/layout/HelpSection'
import { SkipNav } from '@sebt/design-system/src/components/layout/SkipNav'
import type { StateCode } from '@sebt/design-system/src/lib/state'
import { usePathname } from 'next/navigation'
import type { ReactNode } from 'react'

interface CheckerShellProps {
  children: ReactNode
  state: StateCode
}

/**
 * Renders the standard checker chrome (skip nav, maintenance banner, header, help,
 * footer) on every route, including the outage page. The maintenance banner is
 * withheld on the outage route; announcing upcoming maintenance is redundant on
 * the maintenance page itself.
 */
export function CheckerShell({ children, state }: CheckerShellProps) {
  const pathname = usePathname()
  const isOutagePage = pathname === OUTAGE_PATH

  return (
    <>
      <SkipNav />
      {!isOutagePage && <MaintenanceBanner />}
      <Header state={state} />
      <main id="main-content">
        <OutageGuard>{children}</OutageGuard>
      </main>
      <HelpSection state={state} />
      <Footer state={state} />
    </>
  )
}
