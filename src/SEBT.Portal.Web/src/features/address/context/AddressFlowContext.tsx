'use client'

import type { ReactNode } from 'react'
import { createContext, useCallback, useContext, useMemo, useState } from 'react'

import type { UpdateAddressRequest } from '../api/schema'

interface AddressFlowContextValue {
  /** The address submitted by the user. Null until form submission. */
  address: UpdateAddressRequest | null
  /** Store the submitted address in context (React state only — never browser storage). */
  setAddress: (address: UpdateAddressRequest) => void
  /** Clear the address from context. */
  clearAddress: () => void
}

const AddressFlowContext = createContext<AddressFlowContextValue | undefined>(undefined)

export function AddressFlowProvider({ children }: { children: ReactNode }) {
  const [address, setAddressState] = useState<UpdateAddressRequest | null>(null)

  const setAddress = useCallback((addr: UpdateAddressRequest) => {
    setAddressState(addr)
  }, [])

  const clearAddress = useCallback(() => {
    setAddressState(null)
  }, [])

  const value = useMemo<AddressFlowContextValue>(
    () => ({ address, setAddress, clearAddress }),
    [address, setAddress, clearAddress]
  )

  return <AddressFlowContext.Provider value={value}>{children}</AddressFlowContext.Provider>
}

/**
 * Access the address flow context. Must be used within AddressFlowProvider.
 */
export function useAddressFlow(): AddressFlowContextValue {
  const context = useContext(AddressFlowContext)
  if (context === undefined) {
    throw new Error('useAddressFlow must be used within an AddressFlowProvider')
  }
  return context
}
