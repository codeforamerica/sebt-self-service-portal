# 9. State Backend Health Checks Report Degraded, Not Unhealthy

Date: 2026-03-17

## Status

Accepted

## Context

State connector plugins register health checks that verify connectivity to their backend systems (DC's SQL database, CO's CBMS API). Initially these checks reported `Unhealthy` when the backend was misconfigured or unreachable. In ASP.NET Core's health check framework, the overall `/health` endpoint status is the worst individual check status — so a single `Unhealthy` check makes the entire endpoint report `Unhealthy`, which can cause container orchestrators to kill and replace the container.

The portal can still serve requests (login, static pages, cached data) when a state backend is down. Recycling the container won't fix a downstream outage and can cascade into a worse situation.

## Decision

State connector health checks always report `Degraded` — never `Unhealthy` — regardless of whether the issue is missing configuration or an unreachable backend. The structured JSON response from `/health` includes per-check descriptions and exception details, so monitoring and alerting can still distinguish between misconfiguration and connectivity failures.

### Alternatives considered

- **Report `Unhealthy` for connectivity failures, `Degraded` for missing config.** More semantically precise, but the operational consequence (container recycling) is undesirable in both cases.
- **Have connectors report `Unhealthy` but remap to `Degraded` at the portal level.** ASP.NET Core's overall status is the worst individual status with no built-in remapping. A portal-side wrapper could intercept results, but adds complexity for the same operational outcome.
- **Make the failure status configurable via appsettings.** Flexibility for future states, but adds a configuration knob that would likely be set once and forgotten — and increases maintenance burden for state partners. Can revisit if needed as more states onboard.

## Consequences

- The `/health` endpoint never returns `Unhealthy` due to a state backend issue, preventing aggressive container recycling.
- Monitoring must inspect the per-check details (description, exception) in the JSON response rather than relying solely on the top-level status to detect backend outages.
- The portal's own health (e.g., its database) can still report `Unhealthy` if needed in the future — this decision applies only to state connector checks.
