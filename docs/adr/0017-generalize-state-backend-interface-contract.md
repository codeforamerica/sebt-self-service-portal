# 0017 — Generalize the State Backend Interface Contract

**Status:** Proposed (draft from spike DC-568)

---

## Context

The current plugin interface shapes are too state-specific, abstracted insufficiently, especially for resolving households by various identifiers or with different coloading/non-coloading semantics. This shortcoming makes it challenging to scale to multiple new states in a timely, low-risk mannger. A better abstraction is needed.

> [!NOTE]
> _Co-loading_ is the delivery mode where a child's Summer EBT benefits are loaded onto an existing SNAP or TANF EBT card rather than a dedicated card. Supporting co-loading generically likely requires resolving households using program identifier types that vary from state to state. Extending the plugin interface for additional program identifier types would make it more state-specific, not less.

**Constraint:** we need a contract general enough for any state to implement, but we can't break DC and CO, which already run on the current current model.

---

## Decision

We've decided to introduce a documented REST API contract as the primary integration surface for state backends, and adopt a _dual-mode_ loading strategy in the portal middleware.

**How it works:**

- **REST client mode** — when `StateBackend:BaseUrl` is configured, the portal uses an HTTP client that implements the new generalized interfaces by calling the state backend's REST API. The portal reads `GET /capabilities` before any data-plane call; backends only declare routes for capabilities they support.
- **Plugin fallback mode** — when `StateBackend:BaseUrl` is absent, the portal falls back to plugin scanning. DC and CO keep working exactly as they do today.

New states implement the standard REST contract. Existing states stay on plugins. Over time, DC and CO may migrate to the standard REST contract and plugins become optional. We will implement a proof-of-concept adapter microservice between Colorado's existing API and the new standard API contract.

**Key design choices inside the REST contract:**

1. **Capabilities-first.** The portal reads `GET /capabilities` before touching any data endpoint. Backends register only what they support. The alternative — attempt the endpoint, handle 501 — was rejected because state teams may not implement 501 consistently, and explicit capability declaration is safer for a government tech audience.

2. **`intent` field on `POST /households/lookup` for co-loading.** `intent: primary` = own household; `intent: coLoad` = find SNAP/TANF household to load onto. A separate co-load endpoint was considered; it was dropped because the lookup semantics are identical and intent is a modifier, not a different operation.

3. **Atomic address updates (200/400/409).** All-or-nothing semantics. 207 Multi-Status was rejected — state teams would bypass the multi-status response shape with standard error middleware, making partial-success responses unreliable in practice.

4. **`X-Sebt-User-Identity` JWT with dual algorithm support (`HS256` / `RS256`).** Sent only when `capabilities.userAssertion.supported: true`. Previously the portal sent this header unconditionally — now it's gated on the declared capability.

5. **`ial` claim as a number enum (1, 1.5, 2).** _IAL 1.5_ is a non-standard level representing enhanced identity verification without biometrics. The `ial` value is a number, not an integer, to accommodate it.

6. **Card replacement idempotency via `Idempotency-Key` header.** 24-hour dedup window. Replay returns 200. 409 means a different pending request exists. An optional status polling endpoint (`GET /cases/{caseId}/card-replacement/{requestId}`) is supported when `capabilities.cases.cardReplacement.statusTracking.supported: true`.

---

## Alternatives Considered

1. 🔴 **Plugins only (status quo)** — add additional/improved co-loading support to the existing plugin interface and require each state to implement it. Ruled out because the plugin shape has no generalized hook for cross-household lookups; extending it potentially pushes more state-specific logic into what should be a shared contract. New states face the same "implement everything" barrier.

2. 🔴 **REST only, immediate migration** — require DC and CO to implement the new REST contract before we ship anything. Ruled out because it would block active development for a migration that doesn't need to happen on that timeline. Neither state is in a position to redeploy their backends on our schedule.

3. 🟢 **Dual-mode (this decision)** — REST client when configured, plugin fallback otherwise. DC and CO are unaffected. New states implement the documented REST contract. Plugins stay on the path until DC/CO are ready to migrate. We demonstrate a path to adapting to the new spec.

---

## Consequences

**Costs:**

- The portal now maintains two code paths — HTTP client and plugin scanning — until/unless DC/CO migrate. That's surface area to keep aligned.
- The `GET /capabilities` pre-flight adds a round-trip per session initialization. It should be cached, but that's an implementation concern that should be tested for performance issues and bugs.
- State backend implementors need to return well-formed capability documents. Any mismatch silently degrades features. We may need to consider how best to detect and alert on this.
- IAL 1.5 is non-standard. If in the future NIST adopts an intermediate level of enhanced identity verification (between IAL 1 and IAL 2), we may need to revisit this specification to align it with federal standard.

**Benefits:**

- New states implement a documented REST contract, decreasing coupling between the portal and state systems.
- The OpenAPI spec becomes the official integration surface — reviewable, mockable, and independently deployable.
- Co-loading support is abstracted, avoiding state-specific branching inside the portal.
- DC and CO continuity is preserved with no changes on their side.

---

## References

- REST API spec: [`docs/openapi.yaml`](../openapi.yaml)
- Technical design document: [`docs/tdd/state-interface-generalization.md`](../tdd/state-interface-generalization.md)
- Jira: [DC-568](https://codeforamerica.atlassian.net/browse/DC-568)
- Related ADR: [0007-multi-state-plugin-approach.md](./0007-multi-state-plugin-approach.md)
