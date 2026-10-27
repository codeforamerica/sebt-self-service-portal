# Assumptions

Facts about the world this code relies on that the code itself can't prove. Claude adds entries as `proposed`; a human sets `confirmed`, `disputed`, or `rejected`, or corrects the text. See `.claude/skills/capture-assumption/SKILL.md` for the format.

## State backends and connectors

- **Assumption:** State connectors are read-only except for mailing address updates, card replacement requests, and contact/communications preferences.
  - **Scope:** All states; connectors and portal use cases
  - **Source:** CLAUDE.md, "State connector data boundaries"
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** Colorado's backend represents every record as an "application"; DC distinguishes cases from applications the way the portal does.
  - **Scope:** CO and DC connectors; domain model
  - **Source:** CLAUDE.md, "Domain model: Cases vs Applications"
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** CO's backend can look up a household by phone only; DC's by email only.
  - **Scope:** CO and DC household lookup
  - **Source:** docs/adr/0020-config-driven-state-backend-adapter.md:15 (ADR status: Proposed); apps/connectors/co/src/SEBT.Portal.StatePlugins.CO/ColoradoAddressUpdateService.cs:87
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** Both current state backends return card data inline in the household lookup; neither supports a per-case card fetch.
  - **Scope:** CO and DC connectors
  - **Source:** docs/adr/0020-config-driven-state-backend-adapter.md:60
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** Household-data requests fan out to state agency APIs that can rate-limit the portal.
  - **Scope:** All states; frontend fetch and refetch policy
  - **Source:** apps/portal/src/SEBT.Portal.Web/src/features/household/api/useHouseholdData.ts:47-49
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** The portal can still serve useful requests when a state backend is down, and the container orchestrator kills containers whose health check reports Unhealthy.
  - **Scope:** All states; health checks
  - **Source:** docs/adr/0026-state-backend-health-checks-report-degraded.md:13
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** Who owns and operates the per-state REST wrappers long term is undecided; the team operates DC's today.
  - **Scope:** DC wrapper; config-driven adapter
  - **Source:** docs/adr/0020-config-driven-state-backend-adapter.md:61 (flagged there as an open question)
  - **Status:** proposed
  - **Last verified:** never

## Colorado (CBMS)

- **Assumption:** CO does not co-load benefits today and will begin co-loading in 2027.
  - **Scope:** CO connector; IAL and self-service rules
  - **Source:** docs/tdd/minimum-ial-determination.md:31, 47 (TDD superseded, but its facts section is declared still valid); docs/tdd/self-service-rules.md:133
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** A successful CBMS update returns `respCd` `00`, not the `200` shown in the OpenAPI spec.
  - **Scope:** CO address update and card replacement
  - **Source:** apps/connectors/co/src/SEBT.Portal.StatePlugins.CO/ColoradoAddressUpdateService.cs:21 (`00` observed in UAT only; production unconfirmed)
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** CBMS echoes a per-row request indicator (`StdReqInd`) on enrollment-check responses, so rows can be correlated by index.
  - **Scope:** CO enrollment checker
  - **Source:** apps/connectors/co/src/SEBT.Portal.StatePlugins.CO/ColoradoEnrollmentCheckService.cs:135-136; contradicted by docs/tdd/enrollment-checker.md:224 ("CBMS API does not echo a correlation ID"), which is likely stale
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** CBMS `EligSrc` values are only CBMS or PK (application-based) and DIRC or CDE (streamlined); any other value is unexpected.
  - **Scope:** CO case/application disaggregation
  - **Source:** apps/connectors/co/src/SEBT.Portal.StatePlugins.CO/Cbms/EligibilitySourceClassifier.cs:7-10
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** CO's "statused by state, no reissue" card status means the same as `DeactivatedByState`.
  - **Scope:** CO card-replacement rules
  - **Source:** docs/tdd/self-service-rules.md:134 (pending PM confirmation)
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** PingOne has no authorize-request parameter that forces credential re-entry.
  - **Scope:** CO authentication and logout
  - **Source:** docs/adr/0028-oidc-rp-initiated-logout.md:13
  - **Status:** proposed
  - **Last verified:** never

## DC

- **Assumption:** DC production runs on IIS with no Redis; only the AWS `dev-dc` environment has Redis.
  - **Scope:** DC deployment; caching and sessions
  - **Source:** docs/adr/0035-aspire-local-dev-orchestrator.md:149, 155
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** In DC production, a partner-owned Cloudflare layer (WAF and edge cache) sits in front of the web app and has served cached responses despite `Cache-Control: no-cache`.
  - **Scope:** DC production; authenticated GET responses
  - **Source:** docs/adr/0016-cache-busting-api-fetch.md:11, 17. docs/adr/0024-server-rendered-i18n-and-locale-prefixed-routes.md:62 describes a CloudFront distribution instead, possibly for a different environment.
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** Co-loaded (SNAP/TANF) benefits in DC cannot be self-serviced through the portal; users must contact FIS or their caseworker.
  - **Scope:** DC self-service rules; co-loaded cohort filter
  - **Source:** docs/tdd/self-service-rules.md:7; docs/adr/0013-co-loaded-cohort-classification.md:17-18
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** DHS has closed DC Summer EBT applications, so DC shows no apply links.
  - **Scope:** DC portal and enrollment checker
  - **Source:** apps/portal/src/SEBT.Portal.Web/src/lib/applyHref.ts:20-21 (DC-701); may change by program year
  - **Status:** proposed
  - **Last verified:** never

## Infrastructure

- **Assumption:** The deployed database is RDS for SQL Server 2022, which supports neither IAM database authentication nor RDS Proxy, and running ECS tasks never pick up refreshed database credentials.
  - **Scope:** AWS deployments (CO, dev-dc)
  - **Source:** docs/adr/0018-db-credential-rotation-strategy.md:11-15, 26-29 (ADR status: Proposed)
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** All states share a single Smarty vendor account.
  - **Scope:** DC and CO address verification
  - **Source:** tofu/modules/sebt_application/main.tf:203-205
  - **Status:** proposed
  - **Last verified:** never

## Users and handoff

- **Assumption:** Some states forbid persisting PII, so the household identifier must be resolvable from the JWT alone.
  - **Scope:** Identifier resolution; which states is unspecified
  - **Source:** docs/adr/0008-oidc-mycolorado-authentication-and-state-auth-context.md:15; apps/portal/src/SEBT.Portal.Infrastructure/Services/HouseholdIdentifierResolver.cs:16
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** Users commonly access the portal from shared machines and networks (library terminals, kiosks, public Wi-Fi).
  - **Scope:** All states; logout and response caching
  - **Source:** docs/adr/0016-cache-busting-api-fetch.md:13
  - **Status:** proposed
  - **Last verified:** never

- **Assumption:** After handoff, a state agency forks this repo, writes its own connector, and maintains the result with its own engineers.
  - **Scope:** Whole repo
  - **Source:** CLAUDE.md, "Write for handoff"; docs/adr/0031-docfx-engineering-documentation-site.md:11
  - **Status:** proposed
  - **Last verified:** never
