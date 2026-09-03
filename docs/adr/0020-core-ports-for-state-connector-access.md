# 20. Core Ports for State Connector Access

Date: 2026-08-14

## Status

Accepted

## Context

The multi-state plugin system ([0023-multi-state-plugin-approach.md](./0023-multi-state-plugin-approach.md)) exposes state-specific behavior through the plugin contract (`SEBT.Portal.StatesPlugins.Interfaces`). UseCases command handlers and Api controllers referenced that contract — and in some cases Infrastructure types — directly. This violated Clean Architecture layer boundaries ([0002-adopt-clean-architecture.md](./0002-adopt-clean-architecture.md)): inner layers depended on an outer-edge vendor contract, and any change to the plugin interfaces rippled straight into application logic. Household data access already avoided this via `IHouseholdRepository`, a Core-owned abstraction implemented in Infrastructure.

## Decision

Extend the same pattern to the remaining state connector operations. Core owns ports in `SEBT.Portal.Core/StateConnector`:

- `IStateEnrollmentCheckService`
- `IStateAddressUpdateService`
- `IStateCardReplacementService`

plus the boundary models they exchange (requests, results, and enums). Infrastructure provides adapters (`PluginEnrollmentCheckService`, `PluginAddressUpdateService`, `PluginCardReplacementService`) that implement the ports by delegating to the loaded plugin's services, mapping between Core models and plugin contract models at the boundary. UseCases and controllers depend only on the Core ports.

## Consequences

- Boundary models are deliberately duplicated between Core and the plugin contract. Core models are the canonical inner-layer shape; the contract can evolve independently for plugin authors.
- Adapters map enums with explicit switches that throw on unmapped members, so contract drift is caught loudly at the boundary instead of silently mapping to a wrong value.
- UseCases no longer references the plugin contract assembly at all.
- Inner layers stay free of vendor and transport concepts, consistent with the existing `IHouseholdRepository` pattern.
- Adding a state connector operation now requires touching three places: the Core port, the plugin contract, and the adapter. This is accepted as the cost of the boundary.
- The Smarty address-verification diagnostics harness (`IAddressVerificationDiagnostics`) is not a Core port: it exists only to replay canned vendor responses, so it stays in Infrastructure and the feature-gated diagnostics controller resolves it from there directly.
