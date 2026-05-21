# Guid Primary Key Migration — Design

**Date:** 2026-04-21
**Status:** Approved (conversation-driven brainstorming, 2026-04-21)

## Goal

Convert the sequential `int IDENTITY` primary keys on three SEBT Portal tables to app-generated UUIDv7 (`Guid`) to improve logging and debugging observability. The existing sequential integers are low-signal in structured logs (easy to conflate, hard to scan), and Guids give every record a globally unique fingerprint that is trivially grep-able.

**Tables:**
- `Users.Id`
- `UserOptIns.Id`
- `DocVerificationChallenges.Id` (internal PK only — `PublicId` is preserved separately as an external contract boundary)

## Non-Goals

- Migrating other int-PK tables (e.g., `EnrollmentCheckSubmissions`, `DeidentifiedChildResults`). Out of scope; ticket asks for exactly these three.
- Preserving existing row data. Tech lead confirmed on 2026-04-21 that no environment has data that must survive the migration.
- Changing any external wire contract. HTTP routes, request/response bodies, JWT claim shapes, and webhook payloads remain byte-identical.
- Consolidating `DocVerificationChallenge.PublicId` into the new `Id`. `PublicId` is a contract boundary — external consumers (frontend session storage, client URLs, API responses) depend on it. It is not DB-/index-specific, and its existence is orthogonal to PK storage strategy.

## Design Decisions

### 1. UUIDv7 via `Guid.CreateVersion7()`

**Decision:** App-generated UUIDv7, assigned in the repository layer when `entity.Id == Guid.Empty`.

**Why:** On .NET 10, `Guid.CreateVersion7()` is built in. Compared to alternatives:
- Random v4 Guids: high clustered-index fragmentation, no sort order.
- SQL Server `NEWSEQUENTIALID()`: monotonic, but requires a post-insert DB round-trip to read the generated value — awkward with EF Core when the app wants to know the ID before `SaveChanges` returns.
- UUIDv7: time-ordered (monotonic within a process), sort-friendly for clustered index pages, generated client-side with no round-trip, already the industry default for new systems.

### 2. Drop-and-recreate migration

**Decision:** One EF migration file that drops and recreates the three tables with `uniqueidentifier` PKs. No data preservation.

**Why:** Confirmed with tech lead that no environment has data to preserve at merge time. A data-preserving migration would add ~150 lines of raw SQL and several transactional edge cases (partial backfill failure, FK-swap sequencing, legacy index cleanup) for zero practical benefit. Destructive migration is ~30 lines of straightforward `DropTable`/`CreateTable` calls and one raw-SQL block for the filtered unique index.

**Risk:** If a consumer environment we don't know about has data, this migration will silently wipe it. The PR description flags this prominently so deployers know to snapshot first.

### 3. Preserve `DocVerificationChallenge.PublicId` as a separate column

**Decision:** Keep `PublicId` (random v4 Guid, generated via `Guid.NewGuid()` at domain-model construction time) as a distinct column alongside the new `Id` (UUIDv7). Preserve the unique index on `PublicId`.

**Why:** `PublicId` is the contract boundary exposed to API consumers:
- `/api/challenges/{id:guid}/start` route parameter
- `/api/id-proofing/status?challengeId=<guid>` query parameter
- `SubmitIdProofingResponse.ChallengeId`
- Frontend session storage of in-flight challenge references
- Socure webhook correlation (indirectly, via `SocureReferenceId`/`EvalId`, but `PublicId` shows up in logs tied to webhook processing)

These contracts are orthogonal to the internal PK storage strategy. Consolidating would save a column but couple the external identifier's lifecycle to the internal PK's lifecycle — a contract change where none was requested. Tech lead feedback: "contracts depend on it, and it's not DB/index specific."

### 4. Hard JWT cutover

**Decision:** On deploy, all existing JWTs become invalid because their `sub` claim is a stringified int. `ClaimsPrincipalExtensions.GetUserId()` switches from `int.TryParse` to `Guid.TryParse` and returns null for legacy tokens, forcing re-authentication.

**Why:** Compatibility shims (dual-parse, grace period) add code complexity to solve a transient problem. JWT lifetimes are short (minutes). Users re-authenticate. Done.

**Implementation note:** `ResolveUserFilter` already returns 401 Unauthorized when `GetUserId()` returns null — the failure mode is clean.

### 5. Single atomic migration file

**Decision:** One migration file (`{timestamp}_ConvertIntPksToGuids.cs`) covers all three tables.

**Why:** EF wraps migrations in a single transaction by default. An all-or-nothing apply is the desired behavior for a destructive schema change. Splitting per table creates interleaved half-migrated states that are harder to reason about.

## Scope: What Does NOT Change

- **State connector repos** (`sebt-self-service-portal-state-connector`, `sebt-self-service-portal-co-connector`, `sebt-self-service-portal-dc-connector`): zero changes. The plugin interfaces exchange household identifiers (email/phone), not portal user IDs. Verified by exhaustive scan.
- **Frontend** (`src/SEBT.Portal.Web`, `src/SEBT.EnrollmentChecker.Web`): zero changes. JWT is an HTTP-only cookie parsed server-side. The only Guid on the wire is `challengeId`, which is already a Guid-shaped string (it's `PublicId`, which we're preserving).
- **Serilog structured logging**: zero changes. Log template fields (e.g., `{UserId}`, `{ChallengeId}`) adapt to whatever type is bound at call time.
- **Swagger/OpenAPI**: zero explicit changes. Swashbuckle regenerates the spec from controller attributes; Guid replaces int in the documented shapes automatically.

## Testing Strategy

- **Unit tests**: xUnit + NSubstitute. Every repository test, handler test, entity test, and model test is updated to use `Guid`/`Guid.Empty` where it previously used `int`/`0`. New tests added for `ClaimsPrincipalExtensions.GetUserId()` covering valid-Guid, legacy-int, and missing-claim cases.
- **Integration tests**: Testcontainers spin up fresh MSSQL instances per run, apply migrations from scratch, validate end-to-end repository behavior against the new schema.
- **Manual smoke per state**: After migration, validate register → login → JWT-has-Guid-sub → opt-in → submit-id-proofing → poll-challenge-status → simulate-webhook-acceptance → user-marked-verified flow on both DC and CO.

## Rollback

If the migration goes bad in a shared environment:
- Before merge: trivial — amend the migration.
- After merge, before deploy: revert the PR.
- After deploy to a shared env with data: `Down()` exists but does not restore data. Recovery path is a DB snapshot taken before deploy; without one, affected environments accept data loss as the cost of the migration.

## Dependencies and Coordination

- No coordination needed with connector teams (zero impact).
- No coordination needed with frontend team (zero impact).
- Deploy order is the standard API deploy; migration auto-applies on startup.
- Tech lead sign-off obtained for destructive migration on 2026-04-21.
