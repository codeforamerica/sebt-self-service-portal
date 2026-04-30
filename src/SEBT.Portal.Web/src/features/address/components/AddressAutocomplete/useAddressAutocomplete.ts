import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'

import { fetchAutocompleteSuggestions, formatSelected } from './smartyAutocompleteClient'
import type { SelectedAddress, SmartySuggestion } from './types'
import { STATE_PREFER_STATES } from './types'

/**
 * Returns true if two suggestion arrays have identical content.
 * Used to avoid updating state (and changing the array reference) when a
 * re-fetch returns the same results — e.g. when the debounce re-fires due to
 * a re-render without any actual change to the search string.
 */
function areSuggestionsEqual(a: SmartySuggestion[], b: SmartySuggestion[]): boolean {
  if (a.length !== b.length) return false
  for (let i = 0; i < a.length; i++) {
    // eslint-disable-next-line security/detect-object-injection -- i is loop-controlled
    const ai = a[i]!
    // eslint-disable-next-line security/detect-object-injection -- i is loop-controlled
    const bi = b[i]!
    if (
      ai.street_line !== bi.street_line ||
      ai.secondary !== bi.secondary ||
      ai.city !== bi.city ||
      ai.state !== bi.state ||
      ai.zipcode !== bi.zipcode ||
      ai.entries !== bi.entries
    ) {
      return false
    }
  }
  return true
}

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
  /** Increments each time the suggestion list is replaced with genuinely new results. */
  suggestionsVersion: number
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
  const [suggestionsVersion, setSuggestionsVersion] = useState(0)
  const [isOpen, setIsOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(false)

  // Ref to allow reading current suggestions synchronously inside async callbacks
  // without adding suggestions to the closure deps of those callbacks.
  // Updated in a layout effect (same pattern as onSelectRef) so it is current
  // before any async callbacks that fire during the same flush.
  const suggestionsRef = useRef(suggestions)
  useLayoutEffect(() => {
    suggestionsRef.current = suggestions
  })

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
        // Abort any in-flight fetch from a previous (longer) search, otherwise
        // its late resolution would write stale suggestions and reopen the
        // listbox after the user has already deleted back under the threshold.
        abortRef.current?.abort()
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
            // Only update suggestions (and bump the version) if content actually changed.
            // This prevents spurious activeIndex resets when a debounce re-fires for
            // the same search string but the results are identical (e.g. when
            // shouldAdvanceTime causes the timer to fire again while the user is
            // navigating the current results with arrow keys).
            const currentSuggestions = suggestionsRef.current
            if (!areSuggestionsEqual(currentSuggestions, results)) {
              setSuggestions(results)
              setSuggestionsVersion((v) => v + 1)
            }
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
            setSuggestionsVersion((v) => v + 1)
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

  return { suggestions, suggestionsVersion, isOpen, isLoading, selectSuggestion, dismiss, open }
}
