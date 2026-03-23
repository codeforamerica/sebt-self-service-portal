# Enrollment Checker

## Problem Statement / Intent

Parents and guardians need to check whether their children are enrolled
in Summer EBT (SUN Bucks) benefits **without logging in**. The existing
DC portal has a multi-step enrollment checker built with .NET Razor, a
stored procedure, and a monolithic architecture. Colorado has a CBMS
SEBT API with a `CheckEnrollment` endpoint but no portal integration.

We need a **multi-state enrollment checking capability** that:

- Delegates state-specific matching/lookup to the plugin system
- Exposes a public, unauthenticated API endpoint
- Supports checking multiple children in a single request
- Persists de-identified submission data for analytics (no PII)
- Is extensible for states with different required fields


------------------------------------------------------------------------

## High-Level Architecture

The enrollment checker follows the existing Clean Architecture layers
and plugin system:

```
Frontend (future)  →  API Controller  →  Use Case Handler  →  Plugin Interface
                                              │
                                              ├── IEnrollmentCheckService (DC impl)
                                              └── IEnrollmentCheckService (CO impl)
```

**Key architectural decisions:**

- **Separate interface**: `IEnrollmentCheckService` is a new plugin
  interface, distinct from `ISummerEbtCaseService`. Enrollment checking
  is a different capability than household data retrieval.
- **Batch by design**: The interface accepts a list of children and
  returns per-child results. Supports both single and multi-child
  checking.
- **Public endpoint**: No authentication required. Rate limiting
  protects against abuse.
- **Correlation IDs**: Each child in a request gets a server-generated
  `Guid` that flows through to the response, enabling precise
  request/response correlation.
- **No PII in persistence**: De-identified records store birth year,
  school name, eligibility type, and result status only.


------------------------------------------------------------------------

## Data Structures

### Plugin Interface

The state connector package defines the contract that each state
plugin implements:

```
IEnrollmentCheckService
    CheckEnrollmentAsync(EnrollmentCheckRequest, CancellationToken)
        → EnrollmentCheckResult
```

### Request Models

```
EnrollmentCheckRequest
├── IList<ChildCheckRequest> Children
└── string? GuardianContactInfo

ChildCheckRequest
├── Guid CheckId                  // server-generated, for correlation
├── string FirstName              // required
├── string LastName               // required
├── DateOnly DateOfBirth          // required
├── string? SchoolName            // required by convention
├── string? SchoolCode            // state-specific identifier
└── IDictionary<string, string> AdditionalFields  // non-null, empty if none
```

### Response Models

```
EnrollmentCheckResult
├── IList<ChildCheckResult> Results
└── string? ResponseMessage

ChildCheckResult
├── Guid CheckId                  // correlates to request
├── string FirstName
├── string LastName
├── DateOnly DateOfBirth
├── EnrollmentStatus Status
├── double? MatchConfidence       // CO provides this, DC may not
├── string? StatusMessage
├── EligibilityType? EligibilityType
├── string? SchoolName            // echoed back or enriched
└── IDictionary<string, object> Details  // non-null, empty if none
```

### Enumerations

```
EnrollmentStatus: Match, PossibleMatch, NonMatch, Error

EligibilityType: SNAP, TANF, FRP, DirectCert, Unknown
    (extensible as states report different eligibility categories)
```

### Design Conventions

- All public collection types use interfaces (`IList<T>`,
  `IDictionary<K,V>`), not concrete types.
- All dictionary fields are non-nullable. An empty dictionary
  represents "no additional data", not null.
- `SchoolName` is nullable at the type level but required by
  convention for the minimum viable flow. Feature flags can relax
  this per state.


------------------------------------------------------------------------

## API Contract

### Endpoint

```
POST /api/enrollment/check
```

- **Authentication**: None (public endpoint)
- **Rate limiting**: Per-IP fixed window policy

### Request Body

```json
{
  "children": [
    {
      "firstName": "Jane",
      "lastName": "Doe",
      "dateOfBirth": "2015-03-12",
      "schoolName": "Lincoln Elementary",
      "schoolCode": null,
      "additionalFields": {}
    }
  ]
}
```

### Response Body

```json
{
  "results": [
    {
      "checkId": "a1b2c3d4-...",
      "firstName": "Jane",
      "lastName": "Doe",
      "dateOfBirth": "2015-03-12",
      "status": "Match",
      "matchConfidence": null,
      "eligibilityType": "SNAP",
      "schoolName": "Lincoln Elementary",
      "statusMessage": null
    }
  ],
  "message": null
}
```

### Status Codes

| Code | Meaning                              |
|------|--------------------------------------|
| 200  | Check completed (even if all NonMatch) |
| 400  | Validation failure                   |
| 429  | Rate limited                         |
| 503  | Plugin or backend error              |

### Design Notes

- `CheckId` is generated server-side in the use case layer, not
  passed by the client.
- `Details` from the plugin response is NOT exposed in the API
  response. It contains internal state-specific data.
- `dateOfBirth` is an ISO 8601 string in the API contract, parsed
  to `DateOnly` at the use case boundary.
- The `GuardianContactInfo` field on the plugin interface is not
  exposed in the public API request. It exists for future
  authenticated flows where context may be derived from session data.


------------------------------------------------------------------------

## Plugin Implementations

### DC: `DcEnrollmentCheckService`

- MEF-exported with `[ExportMetadata("StateCode", "DC")]`
- Gets connection string from `IConfiguration` (same pattern as
  `DcSummerEbtCaseService`)
- Calls stored procedure `dbo.sp_CheckEligibility` for each child
- Maps stored procedure results to `ChildCheckResult`
- Status mapping: enrolled → `Match`, not found → `NonMatch`
- `EligibilityType` mapped from proc result if available

### CO: `ColoradoEnrollmentCheckService`

- MEF-exported with `[ExportMetadata("StateCode", "CO")]`
- Uses existing Kiota-generated CBMS API client
- Maps `ChildCheckRequest` fields to `CheckEnrollmentRequest`:
  - `FirstName` → `StdFirstName`
  - `LastName` → `StdLastName`
  - `DateOfBirth` → `StdDob`
  - `SchoolCode` → `StdSchlCd`
- Calls `CbmsSebtApiClient.Sebt.CheckEnrollment.PostAsync()`
- Maps `CheckEnrollmentStudentDetail` back:
  - `StdntEligSts` → `EnrollmentStatus`
  - `MtchCnfd` → `MatchConfidence`
- Correlation: Match response items to requests by name + DOB
  (CBMS API does not echo a correlation ID)

### Plugin Registration

Added to `ServiceCollectionPluginExtensions.CreateContainerConfiguration()`:

```csharp
conventions
    .ForTypesDerivedFrom<IEnrollmentCheckService>()
    .Export<IEnrollmentCheckService>()
    .Shared();
```

Each plugin class is a separate MEF export (one interface per class),
following the existing pattern of separate classes for
`ISummerEbtCaseService`, `IStateAuthenticationService`, etc.


------------------------------------------------------------------------

## De-identified Persistence

Enrollment check submissions are logged for analytics without
storing PII. No child names, full dates of birth, or addresses
are persisted.

```
EnrollmentCheckSubmission (EF Core entity)
├── Guid SubmissionId
├── DateTime CheckedAtUtc
├── int ChildrenChecked
├── string? IpAddressHash               // hashed, not raw
├── IList<DeidentifiedChildResult>
│   ├── int BirthYear                   // year only
│   ├── string Status                   // Match, NonMatch, etc.
│   ├── string? EligibilityType
│   └── string? SchoolName
```

Structured logging also emits per-request log entries with the
same de-identified fields for observability.


------------------------------------------------------------------------

## Repos Affected

| Repo | Changes |
|------|---------|
| `sebt-self-service-portal-state-connector` | New `IEnrollmentCheckService` interface, request/response models, enums. NuGet package version bump. |
| `sebt-self-service-portal` | Plugin registration, use case handler, API controller, API models, persistence, EF migration, rate limiting, tests. |
| `sebt-self-service-portal-dc-connector` | `DcEnrollmentCheckService` implementation + tests. |
| `sebt-self-service-portal-co-connector` | `ColoradoEnrollmentCheckService` implementation + tests. |

### Build Order

The state connector NuGet package must be built and published first,
since all other repos depend on it:

```
1. state-connector  → build + publish to ~/nuget-store/
2. dc-connector     → restore, implement, build
3. co-connector     → restore, implement, build
4. main portal      → restore, implement, build + test
```


------------------------------------------------------------------------

## Out of Scope

- Frontend enrollment checker application
- Figma design integration
- SSR/SSG deployment configuration
- CAPTCHA or bot protection beyond rate limiting
- Modifications to existing dashboard or household data flow
