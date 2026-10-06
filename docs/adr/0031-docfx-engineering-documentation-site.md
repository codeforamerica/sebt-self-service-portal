# 31. A published engineering documentation site, built with docfx

Date: 2026-09-24

## Status

Accepted.

## Context

This project is written to be handed off. A state adopts the portal by forking it and writing one connector, and from then on its own engineers maintain the result. Two audiences need to understand the system without us in the room: a partner agency's engineers taking ownership, and a new developer joining the team. Both need the same three things — how to run and develop it, how to maintain and configure it, and why it is built the way it is.

The material mostly existed. `docs/adr/` held thirty decision records, the C# carried `///` comments, and the API served an OpenAPI document. What did not exist was a way to read any of it as one thing. A newcomer had to know the repository well enough to find `docs/`, read Markdown on GitHub with no navigation or search, and separately clone and build to see the API surface. Reference material was effectively invisible: nothing rendered the `///` comments, and the OpenAPI document could only be seen by running the app.

The material had also drifted in ways nothing could catch. Records used four different header formats, four had no date, and six numbers were reused across parallel branches — three records each at 7, 9, and 12, plus pairs at 4, 15, and 18 — so a citation like "ADR 0012" was ambiguous. Once a site was building, twelve warnings surfaced: four broken links, and eight unresolvable `cref`s of which three were substantively wrong documentation rather than formatting slips. `SummerEbtCase.CardStatus` does not exist (the member is `EbtCardStatus`; `CardStatus` is its type), `AddCheck` is an extension method rather than a member of `IHealthChecksBuilder`, and `PiiEncryptionSettings` referenced a type in `SEBT.Portal.Infrastructure`, which `SEBT.Portal.Core` does not reference and by design never can. Each would have misled exactly the reader this work is for.

docfx suits this repository specifically because it reads `///` comments from C# source with Roslyn, so the API reference is generated from the code a maintainer is already expected to keep accurate, rather than from a parallel document that rots.

## Decision

Build a single site under `docs/docfx/` covering decision records, the .NET API reference, and the REST reference; publish it from CI to GitHub Pages.

**Sources stay where they are, and are copied in.** `scripts/docs/generate-doc-sections.ts` copies `docs/adr/` into the site and writes its `toc.yml`. The copy is load-bearing rather than incidental: docfx can map an outside directory with a `src`/`dest` content rule, but a TOC pointing at mapped files resolves neither each page's H1 (every entry renders unnamed) nor its output path (hrefs stay `.md`), and links between records break the same way. `docs/adr/` remains authoritative; copies are never edited and stale ones are cleared each run.

**The .NET reference is generated from source.** `docfx metadata` runs Roslyn over the projects listed in `docfx.json`; `filterConfig.yml` excludes EF Core migrations and anything outside the `SEBT.` namespace root. Improving these pages means improving the `///` comments. The DC connector is absent because it lives in its own repository; it implements the same contract the CO connector does, so the contract pages describe it accurately regardless.

**The REST reference renders OpenAPI 3 directly.** docfx's own `RestApiDocumentProcessor` identifies a spec by a top-level `swagger` property and so reads Swagger 2.0 only; serializing down to 2.0 was tried first and drops what 2.0 cannot express, including `oneOf` and multiple named examples. Instead `rest/index.md` hosts RapiDoc against the document the API actually serves. That document is exported by an xUnit test rather than by Swashbuckle's CLI: both `dotnet swagger tofile` and the build-time `GetDocument` tool start the app through `HostFactoryResolver`, which reaches plugin registration and throws without `PluginAssemblyPaths`, and supplying one makes the tool load the plugin directory into its own assembly load context where `System.Composition.Runtime` fails to bind. `PortalWebApplicationFactory` already solves host configuration for the integration tests, so the export rides on it. Separately, `IncludeXmlComments` was never wired into SwaggerGen, so every operation had come through undescribed; fixing that improves the dev Swagger UI as much as this site.

**Decision records were normalized once.** Every record now uses `# N. Title`, a `Date:` line, then `## Status`; missing dates were recovered from the commit that added each file. Numbers are unique across 0001-0034: in each collision the earliest-dated record kept its number and the others moved to unused numbers (0022, 0025-0030, 0032, 0034), and `main`'s own 0022 moved to 0033 when it collided with this branch. The tradeoff is that moved records are chronologically out of sequence.

**Warnings fail the build.** `--warningsAsErrors` is passed to both `docfx metadata` and `docfx build`, set in the `docs:api` and `docs:render` scripts rather than in CI so a laptop build fails exactly where CI does. Note docfx prints `Build succeeded with warning.` as its last line and then exits 255; the exit code is authoritative.

**docfx stays optional.** Its version is pinned in `docs/docfx/.config/dotnet-tools.json`, scoped to that directory and marked `isRoot` so a repository-wide `dotnet tool restore` never sees it. `pnpm docs:tools` is the entire prerequisite. Nothing in ordinary setup installs it.

**CI publishes it.** `.github/workflows/docs.yaml` builds on every pull request and publishes to GitHub Pages on pushes to `main`. Checkout uses `fetch-depth: 0`, because page dates come from `git log -1 --format=%cI` per source file and at depth 1 every page would silently report the date of the last push; and `persist-credentials: false`, because the job runs pull-request-authored code. Tool versions are read from `global.json` and `engines` in `package.json` rather than written into the workflow. `docs:manifest` publishes a sha256 manifest with the site so a pull request can diff its own build against what is actually deployed, then comments the changed paths and applies a `docs-site-change` label. Commenting and labeling use the `sebt-docs-bot` GitHub App, whose credentials are fetched from Doppler's `ci` config over OIDC; reading the build artifact and the published manifest uses the job's own `GITHUB_TOKEN`. Fork and Dependabot pull requests skip the diff, since they receive no Actions secrets.

## Consequences

- A partner or a new developer has one URL that answers "how do I run this", "how do I configure it", "what does this endpoint return", and "why is it like this". The site is public, at `https://codeforamerica.github.io/sebt-self-service-portal/`, which suits a project states are meant to adopt but means a page is published by writing it, with no separate review step.
- Reference quality is now a property of the code. An undocumented public type is visibly undocumented on the site, and a wrong `cref` fails the build. The cost is that a `///` edit can change many generated pages, which is why the manifest diff exists — a Markdown diff does not show what a reviewer is actually shipping.
- The site cannot silently rot. The same run that publishes it compiles ten C# projects and runs the OpenAPI export test, so a stale link, a renamed target, or a broken Swagger document fails a pull request. This bites immediately and correctly: merging `main` surfaced two links in its newly added CI record (now [ADR 0033](0033-dedupe-build-and-test-across-states.md)) pointing at `0004-state-based-ci-architecture.md`, a record this branch had renumbered (now [ADR 0032](0032-state-based-ci-architecture.md)).
- **Renumbering collides with concurrent work, and did.** While this branch moved nine records, `main` independently added records at 0022-0024, so the merge produced three duplicate numbers. They were resolved by moving the multi-state plugin record to 0022 and three records to 0032-0034, including `main`'s 0022 (now 0033), with inbound links updated. Any future renumbering should expect the same hazard; prefer citing records by filename, as CLAUDE.md already instructs.
- Every pull request to `main` pays a docs build of roughly two to three minutes. Path-filtering was considered and rejected: the API reference derives from `apps/**`, so a correct filter would cover most of the repository.
- Upgrading docfx is a deliberate commit against a pinned version, so a template change between versions cannot arrive unattributed.
- Four things on the REST page are load-bearing and easy to undo by accident — the wrapping `<div>`, the `height: auto !important` rule, the injected shadow styles, and the search-hash cleanup. `docs/docfx/README.md` records why each exists; the page breaks in a non-obvious way without them.
- The `Build site` check needs marking required in branch protection once it has run on a real pull request, the same manual follow-up [ADR 0033](0033-dedupe-build-and-test-across-states.md) records for its own check rename.

## References

- `docs/docfx/README.md` — how every section is wired, and the failure modes behind each choice.
- `docs/docfx/docfx.json`, `filterConfig.yml` — site configuration and what reaches the API reference.
- `scripts/docs/` — the section, REST spec, search index, and manifest generators, with tests.
- `.github/workflows/docs.yaml` — the build, diff, and deploy jobs.
- `docs/docfx/adr/index.md` — the template new decision records follow.
