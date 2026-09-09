'use client'

import { createContext, useContext } from 'react'

const HouseholdCardDetailsLoadingContext = createContext(false)

export function HouseholdCardDetailsLoadingProvider({
  value,
  children
}: {
  value: boolean
  children: React.ReactNode
}) {
  return (
    <HouseholdCardDetailsLoadingContext.Provider value={value}>
      {children}
    </HouseholdCardDetailsLoadingContext.Provider>
  )
}

export function useHouseholdCardDetailsLoading(): boolean {
  return useContext(HouseholdCardDetailsLoadingContext)
}
