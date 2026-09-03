# .NET API Reference

Reference documentation for the portal's C# types, extracted from source by `docfx metadata`. These pages reflect the
branch the site was built from. The prose on each page is the `///` comment from the code. If a type reads as
undocumented here, it is undocumented in the source.

## Layers

The portal follows Clean Architecture (see
[ADR: Adopt clean architecture](../adr/0002-adopt-clean-architecture.md)). Dependencies point inward: the layers below
are listed outermost first, and each one may reference only the ones beneath it.

| Namespace root | Layer | What lives here |
| --- | --- | --- |
| `SEBT.Portal.Api` | Entry point | Controllers, middleware, plugin loading, options wiring. |
| `SEBT.Portal.UseCases` | Application | Command and query handlers for auth, households, enrollment checks. |
| `SEBT.Portal.Core` | Domain | Domain models, service interfaces, exceptions, settings. Defines abstractions; knows nothing about HTTP. |
| `SEBT.Portal.Infrastructure` | Infrastructure | EF Core `DbContext`, repositories, service implementations, external integrations. |
| `SEBT.Portal.Infrastructure.Seeding` | Infrastructure | Development-only data seeding. |
| `SEBT.Portal.Kernel`, `SEBT.Portal.Kernel.AspNetCore` | Cross-cutting | Base classes and ASP.NET Core extensions shared across layers. |
| `SEBT.Portal.StatesPlugins.Interfaces` | Contract | The MEF plugin contract every state connector implements. |
| `SEBT.Portal.StatePlugins.CO` | Connector | The Colorado connector implementation, including its CBMS API client. |

Two things are intentionally absent:

- **The DC connector.** It implements the same `SEBT.Portal.StatesPlugins.Interfaces` contract but lives in the
  separate [`sebt-self-service-portal-dc-connector`](https://github.com/codeforamerica/sebt-self-service-portal-dc-connector)
  repository, so it is not in this build.
- **EF Core migrations.** They are generated code and are filtered out in `filterConfig.yml`. The migration history is
  in git.

## Starting points

- <xref:SEBT.Portal.StatesPlugins.Interfaces>: the contract every state connector implements. Everything a state
  must supply, and the small set of writes a state may accept, is declared here.
- <xref:SEBT.Portal.Core>: the canonical domain model, including the distinction between a case (one child with
  issued benefits) and an application (a guardian-submitted request), which the state backends model differently from
  each other and from the portal.
