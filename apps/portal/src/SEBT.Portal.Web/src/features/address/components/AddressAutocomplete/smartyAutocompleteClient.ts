import type { SmartyAutocompleteResponse, SmartySuggestion } from './types'

const SMARTY_AUTOCOMPLETE_URL = 'https://us-autocomplete-pro.api.smarty.com/lookup'

export interface SmartyAutocompleteParams {
  search: string
  key: string
  preferStates?: string
  selected?: string
}

/**
 * Fetches address suggestions from the Smarty US Autocomplete Pro API.
 * Returns an empty array on any error — autocomplete is an enhancement,
 * not a requirement. Manual entry always works.
 */
export async function fetchAutocompleteSuggestions(
  params: SmartyAutocompleteParams,
  signal?: AbortSignal
): Promise<SmartySuggestion[]> {
  try {
    const url = new URL(SMARTY_AUTOCOMPLETE_URL)
    url.searchParams.set('search', params.search)
    url.searchParams.set('key', params.key)
    if (params.preferStates) url.searchParams.set('prefer_states', params.preferStates)
    if (params.selected) url.searchParams.set('selected', params.selected)

    const response = await fetch(url.toString(), { signal: signal ?? null })
    if (!response.ok) {
      console.warn(`[AddressAutocomplete] Smarty API returned ${response.status}`)
      return []
    }

    const data: SmartyAutocompleteResponse = await response.json()
    return data.suggestions ?? []
  } catch (error) {
    // AbortController cancellations are expected (user typing fast, component unmount) — don't log those.
    if (error instanceof DOMException && error.name === 'AbortError') return []
    console.warn('[AddressAutocomplete] Smarty API request failed:', error)
    return []
  }
}

/**
 * Formats a multi-unit suggestion into the `selected` parameter value
 * for Smarty's secondary (unit) lookup.
 *
 * Format: "{street_line} {secondary} ({entries}) {city} {state} {zipcode}"
 * When secondary is empty, it is omitted.
 */
export function formatSelected(suggestion: SmartySuggestion): string {
  const parts = [suggestion.street_line]
  if (suggestion.secondary) parts.push(suggestion.secondary)
  parts.push(`(${suggestion.entries})`)
  parts.push(suggestion.city)
  parts.push(suggestion.state)
  parts.push(suggestion.zipcode)
  return parts.join(' ')
}
