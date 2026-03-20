# 9. Vendor-Agnostic Privacy-Aware Data Layer

Date: 2026-03-16

## Status

Accepted

## Context

The portal needs analytics and observability to understand user behavior, measure feature adoption, and support operational monitoring. Multiple analytics vendors (e.g., Mixpanel, Google Analytics, Adobe Analytics) may be used across different state deployments, and vendor choices may change over time. Directly integrating vendor SDKs throughout the codebase would create tight coupling, make vendor switches expensive, and risk leaking PII to vendors that should not receive it.

We need a single, canonical source of truth for page metadata, user attributes, and tracked events that:

- Decouples application code from any specific analytics vendor.
- Enforces privacy scoping so PII is only accessible to authorized consumers.
- Supports multiple concurrent vendor integrations without code changes.
- Is understandable and maintainable by state partner agencies after handoff.

## Decision

We adopt a **vendor-agnostic data layer** following the [W3C Customer Experience Digital Data Layer (CEDDL)](https://www.w3.org/2013/12/ceddl-201312.pdf) recommendations. The implementation lives in `src/SEBT.Portal.Web/src/lib/data-layer.ts` as a framework-agnostic TypeScript class.

**Canonical data structure** bound to `window.digitalData`:

- `page` — page-level metadata with `category` and `attribute` sub-objects.
- `user` — user-level data with a `profile` sub-object.
- `event[]` — append-only event log with `eventName`, `eventData`, `timeStamp`, and `scope`.
- `privacy.accessCategories` — declared access scopes.
- `initialized` — boolean flag indicating readiness.

**Privacy-aware scoping:**

- Each data element can be assigned one or more access scopes (e.g., `"default"`, `"analytics"`, `"marketing"`).
- Page data is publicly readable by default (no scope restriction).
- User data automatically receives `"default"` scope, restricting access unless explicitly broadened.
- Scope inheritance walks the path hierarchy from specific to general, so child elements inherit parent scope restrictions.
- Scope metadata is stored in a private `Map`, not on the data objects themselves.

**Loose coupling via DOM CustomEvents:**

- All mutations emit namespaced `CustomEvent`s on `document` (e.g., `digitalData:PageElementSet`, `digitalData:UserProfileSet`, `digitalData:EventTracked`).
- Vendor integration bridges subscribe to these events and forward data according to their scope permissions.
- A global `DataLayer:Initialized` event signals readiness.
- An `eventTypes` object on the root provides type-safe event name constants for bridge consumers.

**React integration** uses a `DataLayerProvider` component that initializes the data layer once on mount via `useRef`, wrapped as the outermost provider in the app layout.

## Consequences

- **Application code** calls `digitalData.page.set(...)`, `digitalData.user.set(...)`, and `digitalData.trackEvent(...)` without knowing which vendors consume the data.
- **Adding a new vendor** requires only a new event listener bridge — no changes to application code or the data layer itself.
- **Removing a vendor** means removing its bridge. No application code changes.
- **PII protection** is enforced at the data layer boundary. A vendor bridge for `"analytics"` scope cannot read user data that only has `"default"` scope.
- **State partners** can configure different vendor bridges per deployment without modifying the core portal.
- **Testing** is straightforward because the class is framework-agnostic and testable with jsdom.

## References

- W3C CEDDL specification: https://www.w3.org/2013/12/ceddl-201312.pdf
- Implementation: `src/SEBT.Portal.Web/src/lib/data-layer.ts`
- Tests: `src/SEBT.Portal.Web/src/lib/data-layer.test.ts`
- Provider: `src/SEBT.Portal.Web/src/providers/DataLayerProvider.tsx`
