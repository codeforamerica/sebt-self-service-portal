---
description: A self-hosted portal that lets families manage their Summer EBT (SUN Bucks) benefits in their own language, on infrastructure that your state already runs.
keywords: summer EBT SUN Bucks portal enrollment checker what is it self-hosted multi-state connector plugin fork multilingual translation feature flags accessibility system requirements components authentication identity proofing deployment IIS AWS
---

# What is the Summer EBT Self-Service Portal?

The Summer EBT Self-Service Portal lets families manage the Summer EBT (SUN Bucks) benefits their state issues to
their children. They can see what has been issued, replace a lost or stolen card, and update a mailing address, in
their own language. Colorado and Washington, DC run it today.

A state adopts the portal by forking this repository and writing one component: a connector to its own systems of
record. The Portal, the Enrollment Checker, and the API behind them stay the same in every state, so your fork can
keep taking changes from here.

> [!NOTE]
> No part of this is hosted by us. The application runs inside your boundary, against your database and your systems
> of record. We supply the software; you supply the environment it runs in.

## Purpose

The goal of this project is to give a state the shortest path from issuing Summer EBT benefits to letting families
manage them online. No state has to build a portal from scratch.

Benefit data never leaves state control. The portal reads it from your systems of record for the request that needs
it, and does not keep a copy.

## Features

- **Families read the portal in their own language**

  English, Spanish, and Amharic ship today. Every string a family sees is content rather than code, so adding a
  language or rewording a screen does not require a code change.

- **It looks like your state**

  Color, type, logo, and button shape all come from a per-state design token file, layered over the U.S. Web Design
  System (USWDS).

- **It holds almost no data**

  The portal's own database has 6 tables, and none of them hold household data, case numbers, benefit amounts, or
  card numbers. Those are read from your systems for the request that needs them and are never written down.

- **It is accessible**

  The Portal and the Enrollment Checker both target WCAG 2.1 AA, and are built from USWDS components that meet that
  standard as a baseline.

- **Two front ends, one API**

  The Portal serves guardians who sign in. The Enrollment Checker answers whether a child is already enrolled and
  needs no account at all. A state can run either one, or both.

- **Screens change without a release**

  Feature flags decide which screens a state shows. They are read from configuration, so changing one does not mean
  shipping new code.

- **One application, different states**

  Colorado and Washington, DC run the same code against different systems of record, with different sign-in methods,
  on different operating systems.

## System requirements

- **.NET 10** and **SQL Server** are required for the API.
  - The application applies its own schema on start-up.
  - SQL Server 2022 is what local development and CI run. Colorado runs Amazon RDS for SQL Server.
- **Node.js 24** is required for the web tier.
- A sign-in method is required. The choice is yours.
  - **OpenID Connect**, against your state's single sign-on. Colorado uses myColorado.
  - Or a **one-time passcode by email**, which needs an email sender. Washington, DC uses this.
- An identity assurance level is required. The source is yours.
  - Your **States single sign-on** can assert one, and the portal trusts it. Colorado works this way.
  - Or the portal calls **Socure** itself. Washington, DC works this way.
- **_Redis_** is optional. Without it the application falls back to an in-memory cache.
- An **_OpenTelemetry_** collector is optional. Nothing is emitted until you set the endpoint.
- **_Linux containers_** or **_Windows Server with IIS_**. Both are in production today.
  - Colorado runs containers on Amazon ECS.
  - Washington, DC needs Windows Server 2019 or later, **IIS**, the **ASP.NET Core 10 Hosting Bundle**, and the
    **HttpPlatformHandler** module.

## Components

- Web server framework:
  - Backend: [ASP.NET Core](https://learn.microsoft.com/aspnet/core) on .NET 10
  - Frontend: [Node.js](https://nodejs.org)
- ORM: [Entity Framework Core](https://learn.microsoft.com/ef/core)
- Database: [SQL Server](https://www.microsoft.com/sql-server)
- Plugin composition: [System.Composition](https://learn.microsoft.com/dotnet/framework/mef/)
- Logging: [Serilog](https://serilog.net)
- Telemetry: [OpenTelemetry](https://opentelemetry.io)
- API documentation: [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- Feature flags: [Microsoft.FeatureManagement](https://github.com/microsoft/FeatureManagement-Dotnet)
- Email: [MailKit](https://github.com/jstedfast/MailKit)
- UI frameworks:
  - [Next.js](https://nextjs.org)
  - [React](https://react.dev)
  - [USWDS](https://designsystem.digital.gov)
  - and various components (see package.json)
- Internationalization: [i18next](https://www.i18next.com) and react-i18next
- Data fetching: [TanStack Query](https://tanstack.com/query)
- Validation: [Zod](https://zod.dev)
- Testing:
  - [xUnit](https://xunit.net), [NSubstitute](https://nsubstitute.github.io), and [Bogus](https://github.com/bchavez/Bogus)
  - [Testcontainers](https://testcontainers.com)
  - [Vitest](https://vitest.dev) and [Playwright](https://playwright.dev)
  - [pa11y](https://pa11y.org) and axe-core
- Infrastructure: [Docker](https://www.docker.com) and [OpenTofu](https://opentofu.org)

## Multi-state design

Four parts are meant to be changed by the state running the portal. Everything else is shared.

- Backend: a state connector, built against a published .NET contract and discovered at runtime
  - Colorado keeps its connector in this repository, Washington, DC in one of its own
  - Writes limited to mailing address, card replacement, and contact preferences
- Frontend:
  - Theme: a design token file per state
  - Content: locale files generated from a per-state spreadsheet export
  - Flow: feature flags, read from configuration and served to the browser by the API
- Tests:
  - Connector contract parity, by enum name and by value
  - Startup validation of the sign-in, token, and encryption settings
  - Per-state connector load and response, against a running API
  - Schema against a real SQL Server
  - WCAG 2.1 AA on every page
  - End-to-end on Chromium, Firefox, and WebKit, desktop and mobile
  - Both states built and tested on every change
- Infrastructure:
  - Linux containers on Amazon ECR and Amazon ECS, defined as OpenTofu modules
  - Windows Server with IIS, shipped as a bundle with a reviewable database package
  - Configuration externalized to environment variables, secrets files, and a per-state overlay

For more detailed information, please refer to:
[Build a state connector](docs/state-connector/index.md)

## Where to go next

- [Overview](docs/overview/index.md) covers the runtime shape, what the portal stores, and the choices a
  state makes.
- [Set up your development environment](docs/local-setup/index.md) gets the whole stack running locally.
- [Build a state connector](docs/state-connector/index.md) gives the contract in full.
- [Change user-facing text](docs/content/index.md) covers the content and translation pipeline.
- [Compliance](compliance/index.md) covers security, privacy, licensing, and accessibility obligations.
- [Architecture Decisions](adr/index.md) record why the system is shaped the way it is.
- [.NET API Reference](api/index.md) covers every published C# type.
- [Releases](releases.md) tell you what changed, and when.
