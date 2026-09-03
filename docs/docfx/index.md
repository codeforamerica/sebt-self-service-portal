---
_layout: landing
---

# Summer EBT Self-Service Portal: Engineering Documentation

Generated from the [`sebt-self-service-portal`](https://github.com/codeforamerica/sebt-self-service-portal)
repository: the decisions and interfaces of the Summer EBT Self-Service Portal, for the engineers who maintain it.

The product has two front doors:

- The **Enrollment Checker** lets families confirm whether a child is already enrolled, without logging in.
- The **Self-Service Portal** lets families log in to view benefit and card status, check application status, update a
  mailing address, and request a replacement card.

Both are served by one ASP.NET Core API, with state-specific behavior supplied by MEF plugins ("state connectors"). As
of Summer 2026 the product is in use by Colorado and Washington, DC.

## What's here

| Section | What it covers |
| --- | --- |
| [Docs](guides/state-connector/index.md) | How to carry out a task against this codebase. Get started covers building a state connector; Content covers changing user-facing text. |
| [Architecture Decisions](adr/index.md) | Every ADR in the repository, and why the system is shaped the way it is. |
| [.NET API Reference](api/index.md) | Types, members, and XML doc comments across the portal's C# projects and the state connector contract. |

## What's not here (yet)

Not yet covered:

- The REST API surface. Run the API locally and use the Swagger UI it serves at `/swagger` in the Development
  environment
- Frontend (Next.js / TypeScript) documentation
- Local development setup and runbooks. See the repository [README](https://github.com/codeforamerica/sebt-self-service-portal#readme)
- The design system
- Test strategy and TDD notes under `docs/tdd/`

Adding a section is a matter of extending `docs/docfx/docfx.json`; see the
[site README](https://github.com/codeforamerica/sebt-self-service-portal/blob/main/docs/docfx/README.md) for how the
build is wired together.

## Where the content comes from

Each section is generated at build time:

- **API reference**, extracted from C# source and XML doc comments by `docfx metadata`. Improving these pages means
  improving the `///` comments in the code.
- **ADRs**. The Markdown files in `docs/adr/` are published as-is, and the navigation is generated from the
  directory listing.
