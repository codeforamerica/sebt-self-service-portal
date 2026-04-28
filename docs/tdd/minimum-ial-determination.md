# Minimum IAL Determination

> **Superseded.** This TDD describes the original `MinimumIal` configuration system,
> which has been unified into `IdProofingRequirements` (see
> [ADR-0012](../adr/0012-unified-id-proofing-requirements.md) and the
> [design spec](../superpowers/specs/2026-04-15-unified-id-proofing-requirements-design.md)).
> The "facts vs. policy" split and plugin boundary design below remain valid —
> only the configuration surface and service layer changed.

## Problem Statement / Intent

Different users need different levels of identity proofing depending on
how their children's benefits were issued. In DC:

- **Application-based cases** (guardian submitted an application): the
  guardian already proved their identity through the application process.
  IAL1 (basic login) is sufficient.
- **Streamline-certified, co-loaded cases** (bulk-imported from state
  systems like SNAP/TANF): the guardian's record was pre-populated. Data
  matching (DOB + SSN/SNAP/TANF ID) is needed, but not full document
  verification. IAL1 is sufficient.
- **Streamline-certified, non-co-loaded cases** (auto-certified but not
  bulk-imported): no prior identity linkage exists. The user must
  complete Socure document verification to reach IAL1+.

The "highest wins" rule applies: if a user has even one non-co-loaded
streamline case among many co-loaded ones, they must reach IAL1+.

Each state may have different policies. CO currently requires IAL1 for
all case types (elevated proofing is per-operation, not per-case-origin).
CO will begin co-loading in 2027.

---

## Design Decisions

### Facts vs. Policy

The core design splits the problem into two concerns:

1. **Facts** (reported by state plugins): each `SummerEbtCase` carries
   two booleans — `IsCoLoaded` and `IsStreamlineCertified`. These are
   objective statements about the case's origin. Plugins set them based
   on state-specific data:
   - **DC**: `IsCoLoaded` = eligibility type is SNAP or TANF;
     `IsStreamlineCertified` = ApplicationId is null.
   - **CO**: `IsCoLoaded` = false (CO doesn't co-load yet);
     `IsStreamlineCertified` = `!IsApplicationBased(EligSrc)` via the
     existing `EligibilitySourceClassifier`.

2. **Policy** (owned by Core, configured per state): `MinimumIalSettings`
   maps each case origin to an IAL requirement. `MinimumIalService`
   evaluates all cases and returns the highest. Policy changes don't
   require code changes — only config updates.

This means the DC plugin never needs to change when IAL requirements
change, and when CO starts co-loading in 2027, they add `IsCoLoaded`
logic to their mapper and configure the policy — no core changes needed.

### Required configuration (no defaults)

`MinimumIalSettings` has no defaults. The app fails to start if the
section is missing from the state overlay. This is intentional: there is
no sensible state-agnostic default, and silently falling back to a
permissive default could be a security issue.

## Data Structures

### Plugin Boundary (`SummerEbtCase`)

Two boolean properties added to the existing model:

```csharp
public bool IsCoLoaded { get; init; }            // default false
public bool IsStreamlineCertified { get; init; }  // default false
```

Defaults are `false` (application-based, not co-loaded) — the
lowest-privilege assumption for plugins that don't set them.

### Configuration (`MinimumIalSettings`)

```csharp
public class MinimumIalSettings
{
    public static readonly string SectionName = "MinimumIal";
    public IalLevel? ApplicationCases { get; set; }
    public IalLevel? CoLoadedStreamlineCases { get; set; }
    public IalLevel? NonCoLoadedStreamlineCases { get; set; }
}
```

All properties are nullable and validated at startup. Example configs:

**DC** (`appsettings.dc.example.json`):

```json
{
  "MinimumIal": {
    "ApplicationCases": "IAL1",
    "CoLoadedStreamlineCases": "IAL1",
    "NonCoLoadedStreamlineCases": "IAL1plus"
  }
}
```

**CO** (`appsettings.co.example.json`):

```json
{
  "MinimumIal": {
    "ApplicationCases": "IAL1",
    "CoLoadedStreamlineCases": "IAL1",
    "NonCoLoadedStreamlineCases": "IAL1"
  }
}
```

### Service Interface

```csharp
public interface IMinimumIalService
{
    UserIalLevel GetMinimumIal(IReadOnlyList<SummerEbtCase> cases);
}
```

Returns `UserIalLevel.IAL1` when the cases list is empty.

---

## Integration: Server-Side IAL Gate

### Security constraint

Household case data must never be returned to a user who has not met
the minimum IAL for their cases. The determination and enforcement
must happen server-side — the frontend cannot be trusted to gate access.

### Approach: 403 with ProblemDetails

`GetHouseholdDataQueryHandler` is the integration point. It already has
the user's current IAL, the full list of cases, and PII visibility
computation. After fetching household data:

1. Compute `minimumIal` via `IMinimumIalService.GetMinimumIal(cases)`
2. Compare against the user's current IAL from JWT claims
3. If insufficient: return an `InsufficientIalResult` (new result type)
4. If sufficient: return household data as before

The controller maps the new result type to a **403 Forbidden** using
the standard `ProblemDetails` format with a `requiredIal` extension:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Insufficient identity assurance level",
  "status": 403,
  "detail": "This household requires IAL1plus. Complete identity verification to access this data.",
  "extensions": {
    "requiredIal": "IAL1plus"
  }
}
```

This is consistent with how other 403 responses are already declared
in `HouseholdController` (e.g., `UpdateAddress`, `RequestCardReplacement`).

### Frontend handling

The dashboard's `useHouseholdData` hook handles the 403:

1. Check response status — if 403, extract `requiredIal` from body
2. Redirect to `/login/id-proofing` (the existing proofing flow)
3. The existing `VerifyOtpForm` post-login routing for first-time users
   (`idProofingStatus === 0`) continues to work for the initial gate

### Why not a JWT claim or separate endpoint?

- A JWT claim (`minimumIal`) would go stale if household composition
  changes after login (e.g., a co-loaded case is added while the user
  is in-session). The 403 check runs on every request.
- A separate endpoint would need its own authorization and still
  require fetching household data internally — duplicating work.
- The 403 approach enforces security at the data boundary: you cannot
  get the data without meeting the IAL. No client-side bypass possible.

---

## What's Not in Scope

- **Data matching for co-loaded households** (DOB + SSN/SNAP/TANF ID
  verification mechanism) — separate ticket
- **Socure challenge trigger** (redirecting user to document verification
  when below minimum IAL) — separate ticket
