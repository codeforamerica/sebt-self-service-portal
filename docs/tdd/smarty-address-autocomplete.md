# Smarty Address Autocomplete

## Problem Statement / Intent

Users entering mailing addresses in the change-address form must type their full address manually, then wait for server-side Smarty validation on submit. This causes:

- **Accuracy issues** — users enter addresses that Smarty rejects, leading to round-trips through the validation/suggestion/not-found flows
- **Friction** — the form is tedious for users who know their address; autocomplete would reduce keystrokes and time-to-completion

We want to add **type-ahead address autocomplete** to the street address field, powered by the Smarty US Autocomplete Pro API, so users can select a verified address as they type.

------------------------------------------------------------------------

## Scope

### In scope

- Autocomplete on the street address line 1 input in the change-address form
- Smarty US Autocomplete Pro API integration (frontend-only, embeddable key)
- Suggestion dropdown as user types (debounced)
- Two-stage selection: building address first, then unit selection for multi-unit buildings (Smarty's `selected` parameter + `entries` count)
- Auto-populate city, state, ZIP, and street address line 2 (unit) on selection
- State/region preference: prefer in-state addresses, with DMV area (DC, MD, VA) preference for DC portal. Allow all US addresses.
- WCAG 2.1 AA accessible combobox pattern

### Out of scope

- Changes to the existing server-side Smarty validation (submit-time)
- Backend API changes (frontend calls Smarty directly with embeddable key)
- International addresses

------------------------------------------------------------------------

## High-Level Architecture

### API integration

- **Smarty US Autocomplete Pro API** called directly from the browser using an embeddable key
- Endpoint: `https://us-autocomplete-pro.api.smarty.com/lookup`
- Key parameters: `search` (user input), `prefer_states` (state preference), `selected` (for secondary/unit lookups)
- Debounce input (250-300ms) to avoid excessive API calls
- Minimum character threshold (3 chars) before triggering suggestions

### Two-stage flow

1. User types in street address field; suggestions appear
2. User selects a suggestion
   - If `entries > 1` (multi-unit building): re-query with `selected` parameter to get unit list, show secondary dropdown
   - If `entries <= 1` (single address): populate all form fields immediately

### State preference configuration

Use Smarty's `prefer_states` parameter, configured per-state:
- **DC portal:** `prefer_states: "DC,MD,VA"` (DMV area)
- **CO portal:** `prefer_states: "CO"`

This biases results toward in-state addresses but does not exclude out-of-state results.

### Environment configuration

New frontend environment variable:
- `NEXT_PUBLIC_SMARTY_EMBEDDED_KEY` — the Smarty embeddable key for autocomplete

------------------------------------------------------------------------

## Component Design

### Open question: USWDS combo-box vs. custom combobox

USWDS ships a `usa-combo-box` component with full ARIA combobox support and keyboard navigation. However, it's designed for static option lists (client-side filtering), not async API-driven suggestions.

**Option A: Extend USWDS combo-box** — Override filtering, feed suggestions dynamically. Pro: visual/behavioral consistency with USWDS. Con: fights the component's design; may require significant overrides.

**Option B: Custom async combobox** — Build from scratch using the same ARIA pattern and USWDS styling. Pro: designed for async data. Con: more code to write and maintain.

**Recommendation:** Investigate Option A first during implementation. If the USWDS component can't cleanly support async suggestions, fall back to Option B using USWDS SCSS for visual styling.

### Component structure (preliminary)

- **`useAddressAutocomplete` hook** — manages API calls, debouncing, suggestion state, two-stage flow
- **`AddressAutocomplete` component** — renders the combobox UI (input + dropdown), handles keyboard navigation
- **Integration in `AddressForm`** — replaces the plain `InputField` for street address line 1

------------------------------------------------------------------------

## Accessibility Requirements (WCAG 2.1 AA)

- `role="combobox"` on the input, `role="listbox"` on the suggestion list
- `aria-expanded`, `aria-activedescendant`, `aria-controls` attributes
- Full keyboard navigation: arrow keys to move, Enter to select, Escape to dismiss
- Screen reader live announcements when suggestion count changes (`aria-live` region)
- Focus management on two-stage transitions (building → unit selection)

------------------------------------------------------------------------

## Testing Strategy

- **Hook unit tests:** debounce behavior, API call parameters (including `prefer_states`), two-stage flow (`entries > 1` triggers secondary query), error handling
- **Component tests:** keyboard navigation, ARIA attributes, suggestion rendering, form population on selection
- **Integration with AddressForm:** verify selecting a suggestion populates all fields, verify manual entry still works when autocomplete is dismissed

------------------------------------------------------------------------

## Dependencies

- Smarty US Autocomplete Pro API access (embeddable key)
- USWDS 3.13 (already installed) for combo-box SCSS
- No backend changes required
- No new npm packages anticipated (standard fetch + debounce)
