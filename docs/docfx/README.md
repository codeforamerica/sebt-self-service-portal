# Engineering documentation site (docfx)

A [docfx](https://dotnet.github.io/docfx/) site that publishes task-oriented guides, the architecture decision
records, the .NET API reference, release pointers, and the compliance pages.

## Prerequisites

docfx is optional. The site is built and published by CI, and nothing else in the repository needs it, so no
part of the normal setup installs it. Run these only to build the site yourself.

```bash
pnpm docs:tools     # restores the pinned docfx into docs/docfx/
pnpm install        # for the section generator
```

The version lives in `docs/docfx/.config/dotnet-tools.json`, a manifest scoped to this directory. It is marked
`isRoot`, so the repository-wide `dotnet tool restore` never picks docfx up.

## Building

```bash
pnpm docs:build     # generate everything, then render to docs/docfx/_site
pnpm docs:serve     # render and serve at http://localhost:8080
```

`docs:build` runs six steps, each of which can be run on its own while iterating:

| Step | Command | What it does |
| --- | --- | --- |
| Sections | `pnpm docs:sections` | Copies `docs/adr/` and `docs/guides/` into the site and writes each `toc.yml`. |
| REST spec | `pnpm docs:spec` | Exports the API's OpenAPI document and stages the RapiDoc bundle. Takes ~20s. |
| .NET API | `pnpm docs:api` | Runs `docfx metadata` over the C# projects. Takes ~15s. |
| Render | `pnpm docs:render` | Renders the site. |
| Search index | `pnpm docs:index` | Rewrites `_site/index.json`. See [Search indexing](#search-indexing). |
| Manifest | `pnpm docs:manifest` | Writes `_site/.docs-manifest.json`. See [Publishing](#publishing). |

`pnpm docs:serve` runs the full build and then serves the result, so the search index step is not skipped.

Both docfx steps pass `--warningsAsErrors`, so a broken link or an unresolvable `cref` fails the build rather
than scrolling past. Note that docfx still prints `Build succeeded with warning.` on its last line and then
exits 255; the exit code is the truth.

## How each section is wired

### .NET API reference

`docfx metadata` reads the C# projects listed in `docfx.json` with Roslyn and writes one YAML file per type into
`api/`. It reads `///` comments **from source**, so `GenerateDocumentationFile` and the resulting XML file are
irrelevant to this site. Improving these pages means improving the comments in the code.

`filterConfig.yml` decides what is documented. It excludes EF Core migrations (generated, and the history is in git)
and anything outside the `SEBT.` namespace root.

The DC connector is absent because it lives in the separate
[`sebt-self-service-portal-dc-connector`](https://github.com/codeforamerica/sebt-self-service-portal-dc-connector)
repository. It implements the same contract the CO connector does, so the contract pages describe it accurately even
though its implementation is not here.

`api/index.md` is hand-written and survives regeneration, because `docfx metadata` only removes files it generated
itself.

### REST reference

`rest/index.md` is an authored page that hosts [RapiDoc](https://rapidocweb.com), a web component that renders
`rest/portal.openapi.json` in the browser. Both the spec and the `rapidoc-min.js` bundle are generated and
git-ignored; `pnpm docs:spec` produces them.

**The spec comes from an xUnit test**, `OpenApiDocumentExportTests`, which asks the in-memory host for the `v1`
document and writes it to the path in `SEBT_OPENAPI_OUTPUT`. Swashbuckle's own tooling was tried first and does not
work here. Both `dotnet swagger tofile` and the build-time `GetDocument` tool start the app through
`HostFactoryResolver`, which runs `Program.Main` as far as the plugin registration on line 28; that throws without
`PluginAssemblyPaths`, and supplying one makes the tool load the plugin directory into its own assembly load context,
where `System.Composition.Runtime` fails to bind against the copy the tool already holds.
`PortalWebApplicationFactory` already solves host configuration for the integration tests, so the export rides on it.
With `SEBT_OPENAPI_OUTPUT` unset the tests still assert the document generates, so breaking Swagger generation fails
the normal suite rather than waiting for a docs build.

**`IncludeXmlComments` is what makes the pages readable.** `GenerateDocumentationFile` was already on, but SwaggerGen
was never told to read the resulting XML, so every operation came through with no description. `ConfigureSwaggerGenOptions`
now wires it up, which is why endpoint summaries and `<response>` text appear here and in the dev Swagger UI. As with
the .NET reference, prose on these pages is the `///` comment from the controller.

**docfx's own REST support is not used.** Its `RestApiDocumentProcessor` identifies a spec by a top-level `swagger`
property, which OpenAPI 3 renamed to `openapi`, so it reads Swagger 2.0 only. Serializing down to 2.0 works and was
the first approach, but it drops what 2.0 cannot express, including `oneOf` and multiple named examples. Rendering
OpenAPI 3 directly keeps the document the API actually serves.

Four things about the page are load-bearing and easy to undo by accident:

- **The wrapping `<div>`.** `rapi-doc` is not a tag markdig recognizes, so an element whose open tag spans several
  lines is escaped into a paragraph instead of passed through. A known block-level tag around it keeps the block raw.
- **`height: auto !important`.** RapiDoc writes `height: 100vh` as an inline style onto its own parent, and an inline
  declaration outranks a normal one. Without `!important` the wrapper stays one viewport tall while the component
  grows past it, and the footer and prev/next links render on top of the reference.
- **The injected shadow styles.** Height, overflow, and the endpoint row's column widths live inside RapiDoc's shadow
  root, which no external stylesheet can select. They are appended once `customElements.whenDefined` resolves, rather
  than polled for, because the bundle is 840KB and its parse time is not predictable.
- **The hash cleanup.** docfx's search appends `?q=<query>` after the fragment, so a search result arrives as
  `#put-/api/household/address?q=mailing address`. RapiDoc matches section ids exactly, so the query is stripped
  before it reads the hash.

The page carries `_disableToc` and `_disableContribution` in `docfx.json`. The sidebar would list one entry and
nothing else, and "Edit this page" would point at a file whose reference content is generated rather than written.

Authentication is absent from the document by design rather than by oversight. The security scheme is contributed by
the state connector through `IStateAuthenticationService.ConfigureSwaggerGenSecurityOptions`; the CO connector adds a
bearer scheme and `DefaultStateAuthenticationService` is a no-op. This site is state-neutral and builds with
`plugins-none`, so no scheme is defined. The page says so rather than implying the endpoints are open.

### Docs and ADRs (copied sections)

`scripts/docs/generate-doc-sections.ts` copies each source directory listed in its `SECTIONS` array into the site and
writes a `toc.yml` listing the files. Adding a section means adding one entry there plus a `toc.yml` nav item. It
does not read ADR contents: TOC entries carry an `href` and no `name`, and docfx fills the name in from each file's
H1, rendering any Markdown in that heading. The script exists because docfx TOC files have no glob support.

Sections with a `parent` share one sidebar, and their `nav` array says where each sits in it. The names are given
explicitly because the H1 trick only works for entries that point at a file, and a heading node points at nothing.
Sections that share a leading name share that heading node, so `Get started` holds every guide filed under it.
The last name in the array labels the section itself, and its pages become that node's children.

`adr/index.md` is authored and tracked in git; the generator preserves it while clearing stale copies. Each guide has
an authored `index.md` that becomes its section landing page. `docs/adr/` and `docs/guides/` remain authoritative,
the copies are never edited, and stale ones are cleared on each run so renamed or deleted files don't linger.

The copy is load-bearing. docfx can map an outside directory in with a `src`/`dest` content rule, but a TOC pointing
at mapped files resolves neither the H1 (all 30 entries render unnamed) nor the output path (hrefs stay `.md`), and
links between records break the same way.

ADRs are sorted by **filename**. Numbers are zero-padded to four digits, so that is also numeric order.

Two rounds of cleanup got the records to a single format, and `adr/index.md` documents the template new ones should
follow:

- **Headers.** All 30 now use `# N. Title`, a `Date:` line, then `## Status`. Six were normalized: five titled
  `# ADR 0007: Title`, one `# 0018 - Title`, one using an inline `**Status:**` line, and four missing a date,
  recovered from the commit that added each file.
- **Numbers.** All 30 are now unique and cover 0001-0030 with no gaps. Nine collided before that (three numbered 7,
  three numbered 9, three numbered 12, and pairs at 4, 15, and 18). In each collision the earliest-dated record kept
  the number and the later ones moved to 0022-0030 in date order, so numbers already cited elsewhere stayed valid.
  The tradeoff is that 0022-0030 are chronologically out of sequence.

## Page dates and search

### Last updated dates

Each copied page carries a "last updated" line below its H1, written by the section generator from
`git log -1 --format=%cI` on the **source** file. The source matters: the copies under `adr/` and `guides/` are
git-ignored, so asking git about one returns nothing. A page that is new and uncommitted gets no stamp rather than an
invented date.

The line is written into the body because docfx discards unknown front matter keys. A `description:` reaches
`<meta name="description">`, but `updated:` and `keywords:` are dropped, which was confirmed by probing the rendered
output rather than assumed.

The site is not versioned. docfx 2.78 has no versioning of its own, with no `versions` key in its schema and no
version flag on the CLI, so publishing more than one release at a time would mean building the mechanism from
scratch. The site describes whatever commit last built it.

### Search indexing

`pnpm docs:index` rewrites `_site/index.json` after the build, fixing two docfx behaviors:

- **Every indexed title ended with the site title**, because the extractor reads `<title>` verbatim. With 553 entries
  carrying "Summer EBT Self-Service Portal Documentation", a search for "portal" or "EBT" matched every
  document on the field lunr weighs most. The suffix is stripped, anchored to the end so the home page keeps its own
  title.
- **The provenance line landed in every summary**, which put a date in each result blurb and made "updated" and
  "source" match every page. It is removed from the indexed copy only; the rendered page keeps it.

Pages may also declare `keywords:` in front matter. docfx drops the key, and this step appends the terms to the
indexed summary, which is what lets the content guide be found by "i18n" or "translation". They go at the end because
lunr scores a term the same wherever it sits, while the search UI shows the front of the summary as the blurb.

The step also adds one entry per REST operation, read from `_site/rest/portal.openapi.json`. RapiDoc renders in the
browser, so the extractor sees an empty article at build time and would otherwise index the page's prose and none of
its 26 endpoints. Each entry points at the id RapiDoc gives the operation's section, `{method}-{path}`. Those ids sit
inside shadow DOM where native fragment navigation cannot reach them, but RapiDoc reads the hash itself and scrolls
to the operation and expands it. The entries are keyed by href, so re-running the step replaces rather than
duplicates them. If the spec is missing the step skips this and says so in its output.

The step is idempotent, so running it against an already-processed index changes nothing.

Ranking caveat: 513 of the 553 indexed pages are API reference against 40 conceptual pages, so a conceptual query can
still surface types ahead of guides. Keywords make a guide appear in results; they do not make it rank first. Field
boosts live in docfx's client bundle and are not reachable from `index.json`.

## Styling

`template/sebt/` is a docfx template layered on top of the built-in `default` and `modern` templates. It contains one
file, `public/main.css`, which replaces the empty override hook `modern` ships.

Most of it remaps Bootstrap 5.3 variables rather than styling components directly, so components the file never names
still pick up the theme. The colors are USWDS system-palette values, each annotated with the token it came from.

This site is **not** part of the USWDS SCSS pipeline that themes the apps, since docfx has no SCSS step, and it is
state-neutral, so it can't adopt either state's theme layer. To track a state rebrand, generate `main.css` from
`packages/design-system/design/states/*.json` rather than editing values by hand.

No webfont is loaded. The apps use Urbanist (DC) and Atkinson Hyperlegible (CO), neither of which is state-neutral.

## What's generated, what's committed

Committed: `docfx.json`, `filterConfig.yml`, `toc.yml`, `index.md`, `compliance/`, `api/index.md`, `adr/index.md`,
`rest/index.md`, `img/`, `template/`, this README.

The `compliance/` pages are authored rather than copied, so they carry no "last updated" line. That matches the other authored
pages. Its dependency figures were measured rather than estimated, and the page says so, because they drift with the
lockfile.

There is no Releases page. Release notes live on GitHub, and the footer links straight to them. An earlier
`releases.md` also documented how the notes are generated, which `scripts/release-notes/README.md` and
`.github/workflows/weekly-release-notes.yml` still cover, and the state release tag patterns
(`YYYY.MM.DD-dc`, `YYYY.MM.DD-colorado`, and the older `-co` spelling), which are now documented nowhere. Read the
tag list itself if you need them. Note that a footer link must be an absolute URL:
`_appFooter` is raw-inserted into the template with `{{{...}}}`, so docfx neither re-renders `{{_rel}}` inside it nor
rewrites a relative href per page, and a relative one would break on every page below the root.

Generated and git-ignored (see `.gitignore`): `_site/`, `api/*.yml`, `adr/*.md` except `index.md`, `adr/toc.yml`,
`guides/`, `rest/portal.openapi.json`, `rest/rapidoc-min.js`.

## Publishing

`.github/workflows/docs.yaml` builds the site on every pull request and publishes it to GitHub Pages on every push
to `main`. Pull requests build but never deploy, which is the point: the build compiles the C# projects and runs the
OpenAPI export test, so a broken `///` comment or a deleted link target fails before merge.

The checkout uses `fetch-depth: 0`. The page dates come from a per-file `git log`, and at the default depth of 1
every page would silently report the date of the last push.

`docs:manifest` writes `_site/.docs-manifest.json`: a sha256 per file, plus one rollup hash over the sorted list.
It publishes with the site, so a pull request can fetch the manifest from the live site and diff its own build
against what is actually deployed, without a retained artifact to expire. Files rewritten on every build regardless
of content — `index.json`, `manifest.json`, `xrefmap.yml` — are excluded, or every comparison would report the whole
site as changed.

When that diff finds anything, CI comments the added, removed, and modified paths on the pull request and applies
the `docs-site-change` label. Both are updated in place on later pushes, and the label is removed if the diff
returns to empty.

## Extending it

Adding a section is a `content` entry in `docfx.json` plus a `toc.yml` entry. Candidates:

- **`docs/tdd/` and `docs/development/`**: already Markdown. Note the copied-section caveat: a `src`/`dest` mapping
  breaks link and title resolution, so add them to `SECTIONS` rather than mapping them.
- **Frontend reference**: docfx does not read TypeScript. This needs TypeDoc output rendered separately or linked.
- **The repository README**: it links to many source paths that docfx would report as broken links, so publishing it
  means either rewriting those links or accepting the warnings.

## Troubleshooting

**`docfx metadata` warns `FailedToLoadAnalyzer: Microsoft.CodeAnalysis.Razor.Compiler`.** Expected and harmless. The
analyzer targets a newer Roslyn than the docfx tool bundles. Metadata extraction does not depend on it.

**`docfx metadata` warns `InvalidCref`.** Real defects in the source's `///` comments: a `<see cref="..."/>`
pointing at something that doesn't resolve from that project. They render as plain text instead of links. Fixing them
means fixing the comment.

**`docfx build` warns `InvalidFileLink` on four links in two ADRs.** Known and expected.
`0027-unified-id-proofing-requirements.md` and `0019-keycloak-local-oidc-stand-in.md` link to `docs/config/ial/`,
`docs/development/`, and `docs/superpowers/specs/`, directories this site does not publish yet, so docfx leaves
those links pointing at `.md` files that aren't there. Publishing `docs/development/` and `docs/config/` would
resolve three of the four. A *new* `InvalidFileLink` warning beyond these is a real broken link.

**`docfx build` warns `EmptyTocItemName`, or ADR nav entries render unnamed.** `adr/` is holding a `toc.yml` without
the copies beside it. Run `pnpm docs:sections`.

**The REST page is blank, or shows only its prose.** The spec or the bundle is missing from `rest/`. Run
`pnpm docs:spec`. The page loads `portal.openapi.json` at runtime, so a missing file is a silent empty render rather
than a build error.

**The REST page renders but the footer sits on top of it.** The `height: auto !important` rule in `rest/index.md` has
been dropped or weakened. RapiDoc sets `height: 100vh` inline on its parent, which wins against a normal declaration.

**`pnpm docs:spec` fails with `PluginAssemblyPaths missing from configuration`.** The export is running against a
host that is not `PortalWebApplicationFactory`. That factory sets `PluginAssemblyPaths__0` to `plugins-none`; see the
REST reference section above for why Swashbuckle's own CLI cannot be swapped in here.
