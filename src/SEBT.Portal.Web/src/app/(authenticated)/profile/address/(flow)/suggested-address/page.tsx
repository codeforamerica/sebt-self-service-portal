'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'

import { useAddressFlow } from '@/features/address'
import { SuggestedAddress } from '@/features/address/components/SuggestedAddress'

export default function SuggestedAddressPage() {
  const { validationResult } = useAddressFlow()
  const router = useRouter()

  useEffect(() => {
    if (!validationResult || !validationResult.suggestedAddress) {
      router.replace('/profile/address')
    }
  }, [validationResult, router])

  if (!validationResult || !validationResult.suggestedAddress) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading...</span>
      </div>
    )
  }

  return <SuggestedAddress />
}
