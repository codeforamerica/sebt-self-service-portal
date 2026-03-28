'use client'

import { usePathname, useRouter } from 'next/navigation'
import type { ReactNode } from 'react'
import { useEffect } from 'react'

import { AddressFlowProvider, useAddressFlow } from '@/features/address'

const FORM_PATH = '/profile/address'

/**
 * Guards downstream flow pages against missing address context.
 * The form page (/profile/address) doesn't need context yet — it populates it.
 * Validation pages (address-not-found, suggested-address) need validationResult but not address.
 * All other pages in the flow (replacement-cards, select) require address data.
 * If neither address nor validationResult is present (e.g., page refresh or direct URL access),
 * redirect to the form.
 */
function FlowGuard({ children }: { children: ReactNode }) {
  const { address, validationResult } = useAddressFlow()
  const pathname = usePathname()
  const router = useRouter()

  const isFormPage = pathname === FORM_PATH
  const needsRedirect = !isFormPage && !address && !validationResult

  useEffect(() => {
    if (needsRedirect) {
      router.replace(FORM_PATH)
    }
  }, [needsRedirect, router])

  if (needsRedirect) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading…</span>
      </div>
    )
  }

  return <>{children}</>
}

export default function AddressFlowLayout({ children }: { children: ReactNode }) {
  return (
    <AddressFlowProvider>
      <FlowGuard>{children}</FlowGuard>
    </AddressFlowProvider>
  )
}
