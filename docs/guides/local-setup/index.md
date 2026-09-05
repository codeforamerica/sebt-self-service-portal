---
description: Run the portal, the enrollment checker, and their supporting services on your own machine.
keywords: local setup environment install prerequisites docker compose mssql mailpit redis jaeger keycloak mock stand-in onboarding getting started dev
---

# Set up your development environment

At the end of this guide you have the API, one web app, and a database running on your machine, with seeded test
households you can sign in as. Nothing you do here touches a state's real systems.

## Pages in this guide

| Page            | What it covers                                                                              |
| --------------- | ------------------------------------------------------------------------------------------- |
| Quickstart      | The shortest path from a clone to a running portal, with a check after each step.           |
| Configuration   | The five files you copy before the first run, and what each one controls.                   |
| Services        | Every container in `compose.yaml`: which are required, which are optional, and their ports. |
| Troubleshooting | Symptoms you hit on a first run, and what each one means.                                   |

## What runs on your machine

Three processes and a set of containers. `pnpm dev:co` starts the first two together.

| Piece                        | Started by             | Address                                  |
| ---------------------------- | ---------------------- | ---------------------------------------- |
| Portal API (.NET)            | `pnpm api:dev`         | `http://localhost:5280`                  |
| Portal web app (Next.js)     | `pnpm web:dev`         | `http://localhost:3000`                  |
| Enrollment Checker (Next.js) | `pnpm dev:co-enroll`   | `http://localhost:3001`                  |
| Supporting services          | `docker compose up -d` | See [Services](#what-stands-in-for-what) |

> [!NOTE]
> The dev server runs over **http**, not https. Both Next.js apps start with `next dev` and the API starts with its
> `http` launch profile.

## What stands in for what

Local development replaces every external dependency with something running on your machine. Most confusing local
behavior comes from forgetting which stand-in you are talking to, so it is worth knowing the whole set.

| In a deployed environment     | On your machine           | What the stand-in gives you                                                                                                                    |
| ----------------------------- | ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| Amazon RDS for SQL Server     | `mssql` container         | A real SQL Server. Migrations apply and test data seeds on start-up, so the schema matches production.                                         |
| Amazon SES                    | `mailpit` container       | Every outgoing email is captured instead of sent. This is how you read a one-time passcode: open <http://localhost:8025> rather than an inbox. |
| Amazon ElastiCache            | `redis` container         | The same distributed cache, with TLS turned on to mirror in-transit encryption. Without it the app falls back to in-memory caching.            |
| The state's OIDC provider     | `keycloak` container      | A sign-in flow you control, so you do not need credentials at a real identity provider.                                                        |
| The observability backend     | `jaeger` container        | An OTLP collector, so traces the app already emits are viewable at <http://localhost:16686>.                                                   |
| The state's systems of record | `MockHouseholdRepository` | Set `UseMockHouseholdData` to `true` and the API stops calling the state connector at all, returning fixed personas instead.                   |

Two of these deserve emphasis on a first run.

**Mailpit is not optional for a sign-in that uses a passcode.** The email is sent, but only Mailpit receives it. If
you are waiting on a code that never arrives, the container is probably not running.

**`UseMockHouseholdData` decides whether a state connector is involved at all.** With it set to `true`, household
data comes from a fixed set of personas in the portal itself, and no connector is loaded for that data. With it set
to `false`, the API calls the connector for the state named in `STATE`. A connector that is absent or unbuilt fails
only in the second case, which is why the same code can appear to work for one person and not another.

Seeded accounts exist regardless. The database seeds `co-loaded@example.com`, `non-co-loaded@example.com`, and
`not-started@example.com`, each at a different stage of identity proofing, and only when no users exist yet.

## Before you start

The commands below assume macOS with [Homebrew](https://brew.sh/).

| Requirement    | Install                        | Used for                                                     |
| -------------- | ------------------------------ | ------------------------------------------------------------ |
| Git            | <https://git-scm.com/install/> | Cloning this repository, and the DC connector if you need it |
| .NET 10 SDK    | `brew install dotnet`          | Building and running the API                                 |
| Node.js        | `brew install node`            | Running both web apps                                        |
| pnpm           | `brew install pnpm`            | Package management and every `pnpm` script in this guide     |
| Docker Desktop | <https://www.docker.com/>      | The containers in the table above                            |

On Windows, enable long paths first with `git config core.longpaths true`. The nested `apps/portal/...` paths can
exceed the legacy 260 character limit.

## Two applications, one API

This repository holds two separate front ends, and they are set up together. One `pnpm install` and one API serve
both.

| Application        | What it is for                                             | Sign-in  | Started by                     |
| ------------------ | ---------------------------------------------------------- | -------- | ------------------------------ |
| Portal             | Benefit and card status, card replacement, address changes | Required | `pnpm dev:co` or `pnpm dev:dc` |
| Enrollment Checker | Confirming whether a child is already enrolled             | None     | `pnpm dev:co-enroll`           |

The two are separate Next.js applications with separate content, but there is no second API to configure. Both call
`SEBT.Portal.Api`, and the enrollment check is a route on it rather than its own service. See the
[.NET API Reference](../../api/index.md) for how the two paths meet.

The commands are alternatives rather than additions. `pnpm dev:co` starts the API and the Portal;
`pnpm dev:co-enroll` starts the API and the Enrollment Checker. Run a second one in another terminal only if you
need both front ends at once, and expect a port clash on the API if you do.

## Which state to run

Pick one before you begin. It decides whether you need a second clone.

| State                | Clones needed                                                                      | Applications available locally                                                                                               |
| -------------------- | ---------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Colorado             | This repository only. The connector is in `apps/connectors/co`.                    | Portal and Enrollment Checker                                                                                                |
| District of Columbia | This repository, plus `sebt-self-service-portal-dc-connector` as a sibling folder. | Portal. The current DC enrollment checker is a [separate application](https://github.com/codeforamerica/cfa-dc-sebt-portal). |

Colorado is the shorter path, and nothing in this guide is Colorado-specific apart from the clone count. Start there
unless you are working on DC.
