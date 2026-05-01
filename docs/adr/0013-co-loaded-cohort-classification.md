# 13. Co-loaded cohort classification and exclusion rule

Date: 2026-04-22

## Status

Accepted

## Context

For MVP, the portal must exclude two groups of households from seeing co-loaded
benefits:

1. **Mixed-eligibility families** — a household that has both co-loaded
   (SNAP/TANF-auto-issued on an existing EBT card) and non-co-loaded (Summer
   EBT-issued) cases. The household should see only the non-co-loaded cases so
   the experience is coherent and so the caseworker remains the authoritative
   contact for the co-loaded benefits.
2. **Applicants with co-loaded benefits** — a household that has co-loaded
   cases and also an **in-flight** application path: either a household-level
   `Application` in `Pending`/`UnderReview`, or a case whose `ApplicationStatus`
   is still `Pending`/`UnderReview`. Historical application rows alone (for example
   `Approved`) do not place the household on the applicant journey for this rule.

Together, this exclusion cohort represents ~3,000 households at rollout.

The ticket required "a configuration-driven mechanism (flag/table/CMS
attribute) … with documented process for updates without code changes." We
evaluated four options:

| Option | Pros | Cons |
| --- | --- | --- |
| Boolean flag on `User` populated by external batch (like existing `User.IsCoLoaded`) | Mirrors existing infrastructure; ops owns the data | Creates a second source of truth that can drift from the actual case data; batch lag |
| New `CohortMemberships` table keyed by household identifier | Flexible; supports other future cohorts | Extra schema, extra joins, extra ops surface for one deterministic rule |
| **Derived at runtime from case + application state** | Single source of truth (the household data itself); no drift; no migration when membership changes | Rule changes require a code change + deploy |
| Config-file list of identifiers | Simple | 3,000 entries is too large to maintain by hand |

We chose the **derived-at-runtime** approach.

## Decision

`SEBT.Portal.Core.Models.Household.CoLoadedCohort` classifies every household
at query time into one of three values, computed on the pre-filter state:

- `NonCoLoaded` — no `SummerEbtCase.IsCoLoaded` is true.
- `CoLoadedOnly` — all cases are co-loaded AND there are no **in-flight**
  household applications (`Applications` entries with status `Pending` or
  `UnderReview`), AND no cases whose `ApplicationStatus` is pending applicant.
  Historical application rows alone (for example `Approved`) do **not**
  trigger this cohort when every case is co-loaded.
- `MixedOrApplicantExcluded` — at least one co-loaded case AND at least one
  of: a non-co-loaded case, an in-flight household application (`Pending` /
  `UnderReview`), or a case with pending-applicant status (`Pending` /
  `UnderReview` on the case).

`GetHouseholdDataQueryHandler` attaches the classification to
`HouseholdData.CoLoadedCohort`, then — for the `MixedOrApplicantExcluded`
cohort only and when `CoLoadedCohortFilter:SuppressCoLoadedCasesForExcludedCohort`
is `true` (the default) — strips co-loaded cases out of the response before mapping it
to the API DTO and realigns `BenefitIssuanceType` with the filtered view.
Setting `SuppressCoLoadedCasesForExcludedCohort` to `false` (via appsettings or Azure App Configuration)
returns the full case list for that cohort while preserving cohort classification for analytics.
Co-loaded-only households retain their cases so the
dashboard isn't empty; per-case `AllowAddressChange` /
`AllowCardReplacement` flags and command-handler guards prevent any
self-service actions on those cases.

The frontend emits the cohort as a standardized user-scoped analytics
dimension — `co_loaded_cohort` with values `non_co_loaded`,
`co_loaded_only`, and `mixed_or_applicant_excluded` — via the data layer on
dashboard load.

## Consequences

### Positive

- No new storage, migrations, or batch pipelines. The classification can
  never drift from the household's actual case state — it is always
  recomputed from fresh data.
- Analytics can segment the three cohorts even though the backend strips
  co-loaded cases from the excluded cohort's payload before it reaches the
  client.
- The rule is expressed in one place (`ClassifyCoLoadedCohort`) and
  exercised by unit tests covering all three values including the
  applicant-with-co-loaded edge case.

### Negative / trade-offs

- **Changing the cohort definition requires a code change.** This is the
  chosen trade-off: the derivation rule is the policy, not a list. If product
  later needs a narrower or broader cohort (e.g., carve out specific
  jurisdictions), we will either tighten the predicate in code or introduce
  a second layer (e.g., a per-jurisdiction feature flag) that composes with
  this rule. Do not add a hand-curated exclusion list alongside this rule —
  that reintroduces the drift and ops burden we rejected above.
- Analytics values are snake_case strings defined in
  `schema.ts#toAnalyticsCohort`, independent of the wire enum. Keep the two
  in sync when adding a new cohort value; schema tests enforce this for the
  current values.

## Update process

To enable or disable **suppression** of co-loaded cases for the excluded cohort (without changing who is classified into each cohort):

1. Set `CoLoadedCohortFilter:SuppressCoLoadedCasesForExcludedCohort` to `false` in environment-specific configuration or Azure App Configuration (reload applies on the options snapshot cadence).
2. Default is `true`, matching the MVP behavior described above.

To adjust the **classification rule**:

1. Open `src/SEBT.Portal.UseCases/Household/GetHouseholdData/GetHouseholdDataQueryHandler.cs`.
2. Edit `ClassifyCoLoadedCohort` (and `IsPendingApplicant` if the applicant
   definition shifts).
3. Update `test/SEBT.Portal.Tests/Unit/UseCases/Household/GetHouseholdDataQueryHandlerTests.cs`
   so each of the three cohort values is covered, including any new edge
   cases the rule change introduces, and scenarios for `SuppressCoLoadedCasesForExcludedCohort` true/false where relevant.
4. If the set of cohort values changes, update:
   - `src/SEBT.Portal.Core/Models/Household/CoLoadedCohort.cs`
   - `src/SEBT.Portal.Web/src/features/household/api/schema.ts`
     (`CO_LOADED_COHORT_MAP`, `CoLoadedCohortSchema`, `toAnalyticsCohort`)
   - `src/SEBT.Portal.Web/src/features/household/api/schema.test.ts`
5. Deploy. The next dashboard load re-classifies every household from the
   current household data; no backfill is required.

## References

- Ticket: "Exclude mixed-eligibility/applicant households from co-loaded views"
- Prior plan: `docs/superpowers/plans/2026-04-15-co-loaded-case-filtering.md`
  (introduced the per-case `IsCoLoaded` signal and per-case action flags
  that this ADR builds on)
- Related: `docs/adr/0012-configurable-self-service-rules.md` (per-case
  action gating uses the self-service evaluator; co-loaded suppression is
  a structural pre-filter that runs earlier in the pipeline)
