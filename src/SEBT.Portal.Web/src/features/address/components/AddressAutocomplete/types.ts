// types.ts — Smarty US Autocomplete Pro API types and shared autocomplete types

/** A single suggestion from the Smarty US Autocomplete Pro API. */
export interface SmartySuggestion {
  street_line: string
  secondary: string
  city: string
  state: string
  zipcode: string
  /** Number of entries at this address. >1 means multi-unit building requiring secondary lookup. */
  entries: number
}

/** Raw API response from Smarty US Autocomplete Pro. */
export interface SmartyAutocompleteResponse {
  suggestions: SmartySuggestion[]
}

/** Address fields populated when the user selects a final suggestion. */
export interface SelectedAddress {
  streetLine1: string
  streetLine2: string
  city: string
  state: string
  zipcode: string
}

/** Per-state prioritized list of state abbreviations for Smarty's prefer_states parameter.
 *  Semicolons rather than commas: the embedded key's dashboard config trips a validator on
 *  any comma-delimited multi-value `*_states` input. Smarty parses semicolons equivalently
 *  for prefer_states and include_only_states, so this delimiter swap preserves the bias
 *  semantics while bypassing the dashboard bug. */
export const STATE_PREFER_STATES: Record<string, string> = {
  dc: 'DC;VA;MD',
  co: 'CO'
}
