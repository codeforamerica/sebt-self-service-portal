# Household Details Data Mapping

This document maps domain attributes for household details data across layers of the SEBT Self-Service Portal: the **UI** (TypeScript), the **Core** portal (C#), and the two state plugin connectors — **CO** (Colorado/CBMS) and **DC** (District of Columbia).

Data flows from state backend systems through the plugin connectors, into the shared Core domain model, through an API response layer, and finally into the frontend TypeScript types.

> **Updated 2026-04-03:** The data mapping was corrected to properly separate Cases (children enrolled to receive benefits) from Applications (requests submitted by guardians). See [UI Gap Analysis](household-data-mapping/ui-gap-analysis.md) for frontend adaptation needs.

## Architecture Context

The portal uses a plugin architecture (MEF/System.Composition) where each state connector implements shared interfaces from the `sebt-self-service-portal-state-connector` package. The key interface for household data is `ISummerEbtCaseService`, which returns a `HouseholdData` object.

**CO** calls the CBMS REST API (Kiota-generated client) and maps abbreviated field names (e.g., `GurdFstNm`, `AddrLn1`) into the shared model via `CbmsResponseMapper`.

**DC** calls a SQL stored procedure (`dbo.GetHouseholdByGuardian`) and maps result rows via an internal `HouseholdMemberRow` intermediary type in `DcSummerEbtCaseService`.

> **Cases vs Applications:** Both connectors now correctly populate `SummerEbtCases` (children enrolled to receive benefits) AND `Applications` (submitted applications). CO uses the `EligSrc` field to classify rows: `DIRC`/`CDE` → auto-eligible cases, `CBMS`/`PK` → submitted applications. DC uses the presence of `SummerEBTCaseID` vs `ApplicationId` columns. A child approved via an application appears in both collections.

---

## Data Mapping Tables

Each section below links to a tab in the companion spreadsheet. Every tab has the same column structure: **Domain Attribute | UI Type.Property | Core Type.Property | CO Type.Property | DC Type.Property | Notes**.

<!-- TODO: Replace SPREADSHEET_URL with the actual Google Sheets URL after importing the TSV files -->

### [Household-Level Attributes](SPREADSHEET_URL#gid=HOUSEHOLD_LEVEL)

Top-level properties on the `HouseholdData` object: email, phone, and benefit issuance type. Key difference: CO looks up households by phone and echoes that back; DC looks up by email and does not return phone.

### [User Profile (Guardian)](SPREADSHEET_URL#gid=USER_PROFILE)

Guardian name fields. Only CO populates these (from `GurdFstNm` / `GurdLstNm` on the CBMS response). DC does not return guardian profile data — the guardian's identity is known only from the auth context.

### [Address On File](SPREADSHEET_URL#gid=ADDRESS)

Mailing address fields. Both connectors populate these, both respect `PiiVisibility.IncludeAddress`. CO concatenates separate `Zip` and `Zip4` fields; DC passes through a single ZIP value.

### [Application](SPREADSHEET_URL#gid=APPLICATION)

The largest mapping surface. Applications represent benefit grants grouped by application ID, each containing one or more children. Notable: four card lifecycle timestamps (`CardRequestedAt`, `CardMailedAt`, `CardActivatedAt`, `CardDeactivatedAt`) exist in the model but neither connector populates them.

### [Child (within Application)](SPREADSHEET_URL#gid=CHILD)

Children listed under each Application. Only first and last name are populated by both connectors. `Child.CaseNumber` is not set by either.

### [SummerEbtCase (CO only)](SPREADSHEET_URL#gid=SUMMER_EBT_CASE)

Per-child, per-case view of Summer EBT data. **Only populated by the CO connector.** DC does not use this collection — all DC data flows through Application instead. This is the richest data model, including card balance, benefit dates, and eligibility details per child.

### Enum Mappings

These tabs show how raw status strings from each state backend are normalized into shared enum values.

- [ApplicationStatus](SPREADSHEET_URL#gid=ENUM_APP_STATUS) — CO uses `"UNDER REVIEW"` (space); DC uses `"UNDER_REVIEW"` (underscore). DC also maps `"IN_PROGRESS"` to `Pending`.
- [CardStatus](SPREADSHEET_URL#gid=ENUM_CARD_STATUS) — Includes a fourth column showing how the UI further collapses these into `UiCardStatus` (Active, Inactive, Processed, Frozen, Undeliverable).
- [IssuanceType / BenefitIssuanceType](SPREADSHEET_URL#gid=ENUM_ISSUANCE_TYPE) — CO hardcodes `SummerEbt` for everything. DC infers from `HouseholdType` and `EligibilityType` string contents.

---

## Observations and Concerns

### ~~Structural asymmetry between connectors~~ (RESOLVED)

Both connectors now populate both `SummerEbtCases` and `Applications`. CO filters by `EligSrc`; DC filters by presence of `SummerEBTCaseID` vs `ApplicationId` columns.

### Semantic mismatch on Benefit Issue Date

`Application.BenefitIssueDate` is mapped from **benefit available date** (`BenAvalDt`) in CO but from **card issue date** (`EbtCardIssueDate`) in DC. These represent different real-world events — when benefits become available vs. when the physical card was issued.

### Unpopulated fields

Four card lifecycle timestamps (`CardRequestedAt`, `CardMailedAt`, `CardActivatedAt`, `CardDeactivatedAt`) exist in the core model but are not populated by either connector. `UserProfile.MiddleName` is also consistently empty. These may be designed for future use or represent abandoned requirements.

### DC does not return guardian profile

DC does not populate `UserProfile` on `HouseholdData`. The guardian's name is not available through DC's stored procedure response, only their email (passed as a lookup parameter).

### Type mismatches across layers

- **Dates:** State-connector interfaces use `DateOnly` / `DateOnly?`. Core uses `DateTime?`. UI uses ISO date strings. Conversions happen at each boundary.
- **Card balance:** CO's CBMS API returns `double?`. Core and state-connector use `decimal?`. The conversion happens in `CbmsResponseMapper`.
- **Application status strings differ:** CO uses `"UNDER REVIEW"` (space). DC uses `"UNDER_REVIEW"` (underscore). Both are normalized by their respective mappers.

### SummerEbtCase.EbtCardStatus is a raw string

Unlike `Application.CardStatus` which uses the `CardStatus` enum, `SummerEbtCase.EbtCardStatus` stores the raw string from the state backend without enum mapping. This means UI code consuming SummerEbtCase card status must handle arbitrary string values.

### UI adaptation needed

The frontend currently reads data in ways that don't fully align with the corrected backend model. See [UI Gap Analysis](household-data-mapping/ui-gap-analysis.md) for the prioritized list of changes needed.
