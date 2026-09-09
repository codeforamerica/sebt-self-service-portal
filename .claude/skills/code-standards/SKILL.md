---
name: code-standards
description: "Use when reviewing code against SEBT's Short, Sharp, Simple standards — after finishing an implementation, before committing or opening a PR, when cleaning up code, or when asked whether code follows project conventions. With no arguments it reviews only the lines changed on the current branch; pass a file or directory to review that path in full regardless of git history."
argument-hint: [file-or-directory]
allowed-tools: Read, Grep, Glob, Bash
---

# SEBT Code Standards Review

Review code against this project's standards. Reference [CLAUDE.md](../../../CLAUDE.md) for cross-cutting conventions and [CONVENTIONS.md](../../../CONVENTIONS.md) for the full Short, Sharp, Simple rules with before/after examples.

## Scoping — CRITICAL

**Path mode** — if `$ARGUMENTS` provides a file or directory, review that path in full, regardless of git history. Every line in it is in scope.

**Diff mode** — with no arguments, the review scope is everything this branch changes, committed or not:

1. Build the scope from three sources, any of which may be empty (a branch with only uncommitted work is normal):
   - `git fetch origin main --quiet`, then `git diff origin/main...HEAD --unified=0` for committed changes (local `main` is often stale — never diff against it)
   - `git diff --unified=0` for uncommitted changes (this also catches new files staged or marked intent-to-add)
   - `git status --short` for untracked files, which are reviewed in full
2. **Only flag issues inside that scope.** Pre-existing code in touched files is out of scope.

**Both modes:**

- If you notice something worth fixing outside the scope, list it under a separate "Out of scope — noticed in passing" section at the end; never mix it into findings.
- Skip repo metadata (`.claude/`, `.github/`, docs) unless `$ARGUMENTS` targets it — this is a code review.
- Reading outside the scope (call sites, tests, configs) to *verify* a finding is encouraged; those files just aren't review targets.

## Priorities (in order)

1. **It works** — baseline
2. **Short** — fewest lines possible, linear flow
3. **Sharp** — solves exactly the problem, no "what if in the future" generalization
4. **Simple** — if it feels complicated, stop and simplify
5. **It's typed** — strict TypeScript, no `any`; C# nullable reference types respected
6. **It's consistent** — follows existing patterns in the codebase

## What to look for

### Hardcoded display strings (critical for this project)

Every user-facing string goes through i18next. Content pipeline: Google Sheet → CSV → `generate-locales.js` → JSON. Never hand-edit the CSV or generated locale JSON files — a missing key is a content gap to note, not something to patch into JSON.

- `<span>Active</span>` → `<span>{t('cardTableStatusActive')}</span>`
- When a DC locale key may be an empty string: `t('key') || 'Fallback'` (i18next returns `''`, not the fallback arg, when a key exists with an empty value)

### Hardcoded colors, spacing, or inline styles (critical for this project)

- No inline `style={...}` props — they bypass per-state theming
- No hex values or magic pixel values — use USWDS utility classes (`margin-top-2`, `bg-success-dark`, `padding-2`) generated from the design tokens
- Reach for `@sebt/design-system` components (`Button`, `InputField`, `Alert`, …) before composing raw HTML + USWDS classes; USWDS component classes before custom CSS; custom SCSS only as a last resort, co-located and referencing `uswds-core` tokens

### Approaches that don't scale across states (critical for this project)

The portal must support many states, not just DC and CO. State-specific behavior lives behind established seams — flag any change that hardcodes a state into shared code:

- `if (state === 'dc')` / `switch` on state codes in shared portal code, frontend or backend. Route the difference through the right seam instead:
  - backend behavior → the state connector plugin (`plugins-{state}/`, MEF)
  - configuration values → `appsettings.{state}.json` overlays (backend) / per-state env config (frontend)
  - copy and content → per-state i18next locale namespaces (Google Sheet pipeline)
  - visual theming → per-state USWDS design tokens
  - feature availability → feature flags
- State quirks leaking into the canonical domain model — connectors disaggregate state-specific shapes in their mapping layer; Core/UseCases stay state-agnostic
- Extending an existing two-state branch with a third hardcoded state — that's evidence the seam is wrong, not a reason to grow the branch
- Assumptions that only hold for one state's backend (e.g., that every case has an application, or that a connector supports write-back beyond the well-known operations)

### Deep call chains

Code should read top to bottom in one place. Flag patterns where understanding a component or handler requires jumping through 3+ helper functions. Prefer a single `STATUS_CONFIG` lookup table over `getLabel()` + `getColor()` + `getIcon()` chains.

**Don't** flatten the established layer boundaries — frontend `component → hook → api client` and backend `controller → use case handler → repository` are load-bearing. But within a single layer, flag orchestrators that only forward calls.

### Functions called only once

Inline it unless the name adds real clarity the expression alone doesn't give (`hasDcCardLifecycle(app)` earns its name; `getStatusLabelKey(uiStatus)` called once does not). Exceptions: controller actions, use case handlers, and test helpers used across multiple tests.

### Types and schemas outside `api/`

Zod schemas belong in the feature's `api/schema.ts`; derived types in `api/index.ts` (`z.infer`). Never define or duplicate domain types in component files.

### Dead code

- Unused exports, types, interfaces — if nothing outside the file imports it, remove `export` (run `pnpm knip` in `SEBT.Portal.Web` when unsure)
- Commented-out blocks — git has history, delete them
- Placeholder C# methods for future states — delete, add when needed
- Jira ticket numbers (DC-###) in code, comments, SQL, or tests — they belong in commit messages and PR descriptions only
- Time-relative naming (`newHandler`, `legacyFlow`, `refactoredService`) — code should be evergreen

### Redundant patterns

- Two `{cardStatus === 'X' && <p>{t('key')}</p>}` branches with the same body → combine with `||`
- Duplicate `if/else` bodies → extract the shared condition
- Multiple imports from the same module → one combined import
- Wrapper components used once that only pass props to a USWDS primitive → inline (extract at 2+ uses)

### Verbose blocks

- TypeScript: single-statement `if` blocks → `if (!cardStatus) return null`; merge related guard clauses
- C#: braces stay even for single-line bodies (Allman style, per CLAUDE.md) — collapse the *logic*, not the braces: merge guard conditions, don't strip `{ }`

### C# Clean Architecture violations

- Inner layers (Kernel, Core, UseCases) must not reference web/HTTP concepts — no ProblemDetails, status codes, headers, or controller types
- No `DbContext` or EF Core expressions in use case handlers — inject the repository interface defined in Core
- New/changed API endpoints need route, HTTP method, and response-type attributes so the Swashbuckle-generated OpenAPI spec stays accurate

### Security & data handling

- No secrets, API keys, or PII (including email addresses in file paths) in committed code
- Identifiers stored only for lookup (cooldowns, dedup) go through `IIdentifierHasher`, never cleartext
- Access control enforced at the API endpoint (403 with structured ProblemDetails), not by filtering in the UI
- Browser-side calls to a new external domain require a CSP directive entry in `SEBT.Portal.Web/src/proxy.ts` — easy to miss because CSP isn't enforced in local dev or tests
- New `NEXT_PUBLIC_*` vars need the full build-time wiring described in CLAUDE.md (env.ts schema, Dockerfile ARG, both deploy workflows) — a var set only at runtime is silently empty in the browser bundle
- Parameterized queries only; no `dangerouslySetInnerHTML` without sanitization

### React Query cache races

When a mutation should update cached data before navigating: `await queryClient.invalidateQueries()` — without `await`, the redirect races ahead and the destination renders stale cache.

### Test conventions

- New functionality without tests is a finding — the project is TDD (xUnit/NSubstitute/Bogus backend, Vitest/RTL frontend)
- .NET test namespaces mirror the implementation namespace (`SEBT.Portal.Tests.Unit.Infrastructure.Services` for `SEBT.Portal.Infrastructure.Services.*`); don't follow the older flat `Unit/Services/` layout
- Use Bogus factories / `createMockApplication`-style factories from `testing/`, not hand-rolled object literals
- Test behavior, not implementation (`screen.getByText(...)`, not component internals)

### Comments

- Preserve existing comments — they're documentation, not clutter
- New comments explain *why* when it isn't obvious; delete comments that restate the code (`// Render the badge`) or talk to the reviewer (`// changed to fix the bug`)

### Exports that should be private

If a function, type, or component is only used within its own file, drop the `export`.

## Output format

For each finding:

- **File and line** (in diff mode, must be a line that appears in the diff)
- **Rule violated** (which principle above)
- **Before** (current code)
- **After** (suggested fix)

If a touched file has zero findings in its changed lines, don't mention it.

Prioritize by impact: **most lines saved first**, then correctness/security, then architecture violations, then style.

End with:

- Total lines that can be removed
- The highest-impact changes (up to 3)
- If there are no findings: say so plainly.
