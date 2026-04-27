# 12. Unified IdProofingRequirements — Replace Dual IAL Config with Resource+Action Model

Date: 2026-04-15

## Status

Accepted

## Context

The portal had two independent configuration sections controlling identity assurance levels (IAL):

- **`IdProofingRequirements`** — per-field PII visibility (e.g., `address+view` requires IAL1plus to see the address)
- **`MinimumIal`** — per-case-type feature access (e.g., `streamline` requires IAL1plus to perform write operations)

These systems were unrelated in code: different settings classes, different services, different validators. No validation enforced coherence between them.

In April 2026, we discovered that Colorado's `MinimumIal` was configured to `IAL1` across all case types, meaning a user at IAL1 could change their address without step-up identity verification — even though `IdProofingRequirements` correctly required IAL1plus to *view* the address. The backend enforcement code was correct; the configuration created a security inversion where write operations had lower IAL requirements than read operations on the same data.

Root causes:
1. `MinimumIalSettings` had no secure defaults (nullable properties, no in-code defaults)
2. No cross-system invariant checked that write-level IAL >= view-level IAL
3. The name `MinimumIal` didn't communicate what it protected
4. Two config sections made the view/write relationship implicit

## Decision

Unify both systems into a single `IdProofingRequirements` config section using `resource+action` keys (e.g., `address+view`, `address+write`, `household+view`, `card+write`).

Key design choices:

- **One config section, one mental model.** Every IAL requirement is a `resource+action` key. Operators don't need to understand the relationship between two separate sections.
- **Coherence validation at startup and on config reload.** A validator enforces `write >= view` for each resource and checks that step-up OIDC configuration is consistent with IAL requirements.
- **Secure defaults.** Every requirement defaults to `IAL1plus`. States explicitly opt *down* where policy allows — never the reverse.
- **Polymorphic values.** A requirement can be a simple string (uniform for all case types) or an object with per-case-type sub-requirements, supporting both simple and granular configurations in one syntax.
- **Fail-safe config reload.** Invalid config pushes (e.g., via AWS AppConfig) are rejected with a Critical log; the service retains the last-known-good configuration rather than serving 500s.

See [design spec](../superpowers/specs/2026-04-15-unified-id-proofing-requirements-design.md) and [configuration guide](../config/ial/README.md) for full details.

## Consequences

- The `MinimumIal` config section, `MinimumIalSettings`, `MinimumIalService`, and `IMinimumIalService` are removed. The existing TDD (`docs/tdd/minimum-ial-determination.md`) is superseded.
- Three use case handlers (`GetHouseholdDataQueryHandler`, `UpdateAddressCommandHandler`, `RequestCardReplacementCommandHandler`) are updated to use the new `IIdProofingService` and `IPiiVisibilityService` interfaces.
- State-specific Tofu configurations (env vars) must be updated from `MinimumIal__*` keys to `IdProofingRequirements__*` keys. This is a coordinated deployment change.
- The configuration inversion that caused the CO security bypass becomes structurally impossible — the coherence validator rejects any config where a write operation requires less identity assurance than the corresponding view operation.
- Custom config binding via `IConfigureOptions<T>` replaces standard `IOptions<T>` binding, trading automatic binding for control over the polymorphic value parsing.
