'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'

import { useAddressFlow } from '@/features/address'
import { AddressNotFound } from '@/features/address/components/AddressNotFound'

export default function AddressNotFoundPage() {
  const { validationResult } = useAddressFlow()
  const router = useRouter()

  useEffect(() => {
    if (!validationResult) {
      router.replace('/profile/address')
    }
  }, [validationResult, router])

  if (!validationResult) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading…</span>
      </div>
    )
  }

  return <AddressNotFound />
}
