'use client'

import type { ReactNode } from 'react'
import { createContext, useCallback, useContext, useMemo, useState } from 'react'

import type { AddressUpdateResponse, UpdateAddressRequest } from '../api/schema'

interface AddressFlowContextValue {
  /** The confirmed address (after validation passes or user accepts). Null until confirmed. */
  address: UpdateAddressRequest | null
  /** Store the confirmed address in context (React state only — never browser storage). */
  setAddress: (address: UpdateAddressRequest) => void
  /** Clear the confirmed address from context. */
  clearAddress: () => void
  /** The validation result from the API when address validation fails. */
  validationResult: AddressUpdateResponse | null
  /** The address the user entered (before validation). */
  enteredAddress: UpdateAddressRequest | null
  /** Store a validation result and the address that was entered. */
  setValidationResult: (result: AddressUpdateResponse, entered: UpdateAddressRequest) => void
  /** Clear the validation result and entered address. */
  clearValidationResult: () => void
}

const AddressFlowContext = createContext<AddressFlowContextValue | undefined>(undefined)

export function AddressFlowProvider({ children }: { children: ReactNode }) {
  const [address, setAddressState] = useState<UpdateAddressRequest | null>(null)
  const [validationResult, setValidationResultState] = useState<AddressUpdateResponse | null>(null)
  const [enteredAddress, setEnteredAddressState] = useState<UpdateAddressRequest | null>(null)

  const setAddress = useCallback((addr: UpdateAddressRequest) => {
    setAddressState(addr)
  }, [])

  const clearAddress = useCallback(() => {
    setAddressState(null)
  }, [])

  const setValidationResult = useCallback(
    (result: AddressUpdateResponse, entered: UpdateAddressRequest) => {
      setValidationResultState(result)
      setEnteredAddressState(entered)
    },
    []
  )

  const clearValidationResult = useCallback(() => {
    setValidationResultState(null)
    setEnteredAddressState(null)
  }, [])

  const value = useMemo<AddressFlowContextValue>(
    () => ({
      address,
      setAddress,
      clearAddress,
      validationResult,
      enteredAddress,
      setValidationResult,
      clearValidationResult
    }),
    [
      address,
      setAddress,
      clearAddress,
      validationResult,
      enteredAddress,
      setValidationResult,
      clearValidationResult
    ]
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
