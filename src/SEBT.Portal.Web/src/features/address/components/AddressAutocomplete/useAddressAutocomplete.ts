import { useCallback, useEffect, useRef, useState } from 'react'

import { fetchAutocompleteSuggestions, formatSelected } from './smartyAutocompleteClient'
import type { SelectedAddress, SmartySuggestion } from './types'
import { STATE_PREFER_STATES } from './types'

const DEBOUNCE_MS = 300
const MIN_CHARS = 3

interface UseAddressAutocompleteOptions {
  /** Current value of the street address input */
  search: string
  /** Portal state code (e.g. 'dc', 'co') — determines prefer_states */
  stateCode: string
  /** Called when the user selects a final address (single entry or unit) */
  onSelect: (address: SelectedAddress) => void
}

interface UseAddressAutocompleteReturn {
  suggestions: SmartySuggestion[]
  isOpen: boolean
  isLoading: boolean
  selectSuggestion: (index: number) => void
  dismiss: () => void
  open: () => void
}

export function useAddressAutocomplete({
  search,
  stateCode,
  onSelect
}: UseAddressAutocompleteOptions): UseAddressAutocompleteReturn {
  const [suggestions, setSuggestions] = useState<SmartySuggestion[]>([])
  const [isOpen, setIsOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(false)

  const abortRef = useRef<AbortController | null>(null)
  const onSelectRef = useRef(onSelect)

  // Keep the ref current on each render without triggering re-renders.
  // Assigned in a layout effect so it is updated before any async callbacks
  // that fire during the same flush.
  useEffect(() => {
    onSelectRef.current = onSelect
  })

  // Track the original search term for secondary lookups
  const searchAtSelectionRef = useRef('')

  const key = process.env.NEXT_PUBLIC_SMARTY_EMBEDDED_KEY ?? ''
  const enabled = key.length > 0
  // eslint-disable-next-line security/detect-object-injection -- stateCode values come from the portal config, not user input
  const preferStates = STATE_PREFER_STATES[stateCode] ?? ''

  // Debounced primary search.
  // All state updates happen inside the setTimeout callback or its promise
  // chain — never synchronously in the effect body — to satisfy the
  // react-hooks/set-state-in-effect lint rule.
  useEffect(() => {
    const timer = setTimeout(() => {
      if (!enabled || search.length < MIN_CHARS) {
        setSuggestions([])
        setIsOpen(false)
        return
      }

      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller

      setIsLoading(true)
      fetchAutocompleteSuggestions({ search, key, preferStates }, controller.signal).then(
        (results) => {
          if (!controller.signal.aborted) {
            setSuggestions(results)
            setIsOpen(results.length > 0)
            setIsLoading(false)
          }
        }
      )
    }, DEBOUNCE_MS)

    return () => clearTimeout(timer)
  }, [search, enabled, key, preferStates])

  const selectSuggestion = useCallback(
    (index: number) => {
      // eslint-disable-next-line security/detect-object-injection -- index comes from our own component, not user input
      const suggestion = suggestions[index]
      if (!suggestion) return

      if (suggestion.entries > 1) {
        // Multi-unit building: fetch individual units
        searchAtSelectionRef.current = search
        abortRef.current?.abort()
        const controller = new AbortController()
        abortRef.current = controller

        setIsLoading(true)
        fetchAutocompleteSuggestions(
          {
            search: searchAtSelectionRef.current,
            key,
            preferStates,
            selected: formatSelected(suggestion)
          },
          controller.signal
        ).then((unitResults) => {
          if (!controller.signal.aborted) {
            setSuggestions(unitResults)
            setIsLoading(false)
            // Keep isOpen true to show unit options
          }
        })
      } else {
        // Single address: select and close
        onSelectRef.current({
          streetLine1: suggestion.street_line,
          streetLine2: suggestion.secondary,
          city: suggestion.city,
          state: suggestion.state,
          zipcode: suggestion.zipcode
        })
        setSuggestions([])
        setIsOpen(false)
      }
    },
    [suggestions, search, key, preferStates]
  )

  const dismiss = useCallback(() => {
    abortRef.current?.abort()
    setSuggestions([])
    setIsOpen(false)
  }, [])

  const open = useCallback(() => {
    if (suggestions.length > 0) setIsOpen(true)
  }, [suggestions])

  // Abort any in-flight request on unmount
  useEffect(() => () => abortRef.current?.abort(), [])

  return { suggestions, isOpen, isLoading, selectSuggestion, dismiss, open }
}
