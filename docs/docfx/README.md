# Engineering documentation site (docfx)

A [docfx](https://dotnet.github.io/docfx/) site that publishes three things from this repository: task-oriented
guides, the architecture decision records, and the .NET API reference.

## Prerequisites

```bash
dotnet tool install -g docfx    # 2.78 or later
pnpm install                    # for the section generator
```

## Building

```bash
pnpm docs:build     # generate everything, then render to docs/docfx/_site
pnpm docs:serve     # render and serve at http://localhost:8080
```

`docs:build` runs three steps, each of which can be run on its own while iterating:

| Step | Command | What it does |
| --- | --- | --- |
| Sections | `pnpm docs:sections` | Copies `docs/adr/` and `docs/guides/` into the site and writes each `toc.yml`. |
| .NET API | `pnpm docs:api` | Runs `docfx metadata` over the C# projects. Takes ~15s. |
| Render | `docfx build` | Renders the site. |

`pnpm docs:serve` skips the generators and only renders, so run it after a `docs:build`, or on its own when the only
thing you changed is Markdown or CSS.

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

### Guides and ADRs (copied sections)

`scripts/docs/generate-doc-sections.ts` copies each source directory listed in its `SECTIONS` array into the site and
writes a `toc.yml` listing the files. Adding a section means adding one entry there plus a `toc.yml` nav item. It
does not read ADR contents: TOC entries carry an `href` and no `name`, and docfx fills the name in from each file's
H1, rendering any Markdown in that heading. The script exists because docfx TOC files have no glob support.

`adr/index.md` is authored and tracked in git; the generator preserves it while clearing stale copies. Guides have no
authored index; the nav lists the guides directly. `docs/adr/` remains authoritative, the copies are never edited,
and stale ones are cleared on each run so renamed or deleted records don't linger.

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

Committed: `docfx.json`, `filterConfig.yml`, `toc.yml`, `index.md`, `api/index.md`, `adr/index.md`, `img/`,
`template/`, this README.

Generated and git-ignored (see `.gitignore`): `_site/`, `api/*.yml`, `adr/*.md` except `index.md`, `adr/toc.yml`,
`guides/`.

## Extending it

Adding a section is a `content` entry in `docfx.json` plus a `toc.yml` entry. Candidates:

- **`docs/tdd/` and `docs/development/`**: already Markdown. Note the copied-section caveat: a `src`/`dest` mapping
  breaks link and title resolution, so add them to `SECTIONS` rather than mapping them.
- **REST API**: docfx's REST API support reads Swagger 2.0 only, while Swashbuckle emits OpenAPI 3.0.1, so this
  means either embedding a renderer or converting the document. Note also that `IncludeXmlComments` is not configured
  anywhere in the solution, so no `///` comments reach the OpenAPI document and every endpoint would render without a
  description. Fix that first, or the section adds an endpoint list and nothing else.
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
