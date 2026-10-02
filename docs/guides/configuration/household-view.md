---
description: Which fields the household dashboard shows, and which self-service actions a household may take.
keywords: dashboard household display fields case number card last4 self-service rules address update card replacement
---

# What households see and can do

Two independent things: which fields appear on the dashboard, and which actions a household is allowed to perform.

## Dashboard fields

Each flag hides or shows one piece of the household view, so a state can omit a field its backend does not populate.

| Flag | Default | Shows |
| --- | --- | --- |
| `show_case_number` | `true` | The case number |
| `show_application_number` | `true` | The application number |
| `show_application_date` | `true` | The application date |
| `show_card_last4` | `true` | The last four digits of the EBT card |
| `show_contact_preferences` | `false` | The contact preferences controls |
| `enable_beta_banner` | `false` | The beta banner |

These default to on, so a state that does not supply a field should turn its flag off rather than leave it. Showing
a field the connector never fills produces a blank row, not an omitted one.

These flags are read by name in the front-end code rather than through the `FeatureFlags` constants class, so a
misspelling fails quietly. Copy the name from an existing overlay rather than typing it.

## Self-service actions

`SelfServiceRules` decides what a household may do without contacting the state. It covers two actions:

| Key | Action |
| --- | --- |
| `AddressUpdate` | Changing the mailing address |
| `CardReplacement` | Requesting a replacement card |

Each takes the same shape:

| Field | Meaning |
| --- | --- |
| `Enabled` | Whether the action is offered at all. Defaults to `true`. |
| `DisabledMessageKey` | The content key explaining why, shown when the action is unavailable |
| `ByIssuanceType` | Per-issuance-type overrides |

`DisabledMessageKey` points at a translation key rather than holding text, so the explanation stays translatable.
See [Change user-facing text](../content/index.md).

Each entry under `ByIssuanceType` narrows the rule further:

| Field | Meaning |
| --- | --- |
| `Enabled` | Whether the action is allowed for this issuance type |
| `AllowedCardStatuses` | Card statuses the action is permitted for |
| `AllowedCaseStatuses` | Case statuses the action is permitted for |

The status lists are allowlists. An empty list permits nothing, so omitting a status is how an action is withheld
for cases in that state.

These rules are a product decision rather than plumbing. Turning an action on requires the state connector to accept
the corresponding write; connectors are read-only by default.

## Which household record is used

| Section | Field | Purpose |
| --- | --- | --- |
| `StateHouseholdId` | `PreferredHouseholdIdTypes` | Which identifier types to look a household up by, such as email or SNAP ID |
| `CoLoadedCohortFilter` | `SuppressCoLoadedCasesForExcludedCohort` | Whether to hide co-loaded cases belonging to an excluded cohort. Defaults to `true`. |

## Loading behaviour

| Flag | Default | Controls |
| --- | --- | --- |
| `defer_ebt_card_data_loading` | `false` | Lets the dashboard load household data first and fetch card fields in a follow-up request |

Useful where fetching card details from the state backend is slow enough to hold up the whole page.
