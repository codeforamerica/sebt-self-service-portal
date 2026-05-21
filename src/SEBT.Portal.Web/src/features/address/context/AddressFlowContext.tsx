'use client'

import type { ReactNode } from 'react'
import { createContext, useCallback, useContext, useMemo, useState } from 'react'

import type { AddressUpdateResponse, UpdateAddressRequest } from '../api/schema'

const DEFAULT_FORM_PATH = '/profile/address'
const DEFAULT_CONTINUE_PATH = '/profile/address/replacement-cards'

interface AddressFlowContextValue {
  /** The address submitted by the user. Null until form submission. */
  address: UpdateAddressRequest | null
  /** Store the submitted address in context (React state only — never browser storage). */
  setAddress: (address: UpdateAddressRequest) => void
  /** Clear the address from context. */
  clearAddress: () => void
  /** Backend validation response (422 with status/reason/suggestedAddress). Null until validation completes. */
  validationResult: AddressUpdateResponse | null
  /** The address the user originally entered, before any backend suggestion. */
  enteredAddress: UpdateAddressRequest | null
  /** Store the validation result and the entered address together after backend response. */
  setValidationResult: (result: AddressUpdateResponse, entered: UpdateAddressRequest) => void
  /** Clear validation state (e.g. when navigating back to the address form). */
  clearValidationResult: () => void
  /** Path to return to when user edits address from suggestion/not-found pages. */
  formPath: string
  /** Path to continue to after an address is accepted. */
  continuePath: string
  /** Update the active address-flow navigation targets. */
  setNavigationTargets: (targets: { formPath: string; continuePath: string }) => void
}

const AddressFlowContext = createContext<AddressFlowContextValue | undefined>(undefined)

export function AddressFlowProvider({ children }: { children: ReactNode }) {
  const [address, setAddressState] = useState<UpdateAddressRequest | null>(null)
  const [validationResult, setValidationResultState] = useState<AddressUpdateResponse | null>(null)
  const [enteredAddress, setEnteredAddressState] = useState<UpdateAddressRequest | null>(null)
  const [formPath, setFormPath] = useState(DEFAULT_FORM_PATH)
  const [continuePath, setContinuePath] = useState(DEFAULT_CONTINUE_PATH)

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

  const setNavigationTargets = useCallback(
    (targets: { formPath: string; continuePath: string }) => {
      setFormPath(targets.formPath)
      setContinuePath(targets.continuePath)
    },
    []
  )

  const value = useMemo<AddressFlowContextValue>(
    () => ({
      address,
      setAddress,
      clearAddress,
      validationResult,
      enteredAddress,
      setValidationResult,
      clearValidationResult,
      formPath,
      continuePath,
      setNavigationTargets
    }),
    [
      address,
      setAddress,
      clearAddress,
      validationResult,
      enteredAddress,
      setValidationResult,
      clearValidationResult,
      formPath,
      continuePath,
      setNavigationTargets
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
