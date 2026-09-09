# 0018 — OpenTelemetry for the Web Tier

**Status:** Accepted
**Date:** 2026-07-15

## Context

The portal is two services: a Next.js Node server and a .NET 10 API. Browser requests proxy through Next.js to .NET. The backend already traces with the OpenTelemetry SDK as `sebt-portal-api` (`SEBT.Portal.Api/Telemetry/`), but the Next.js hop was uninstrumented — so the proxy's call to the backend started a fresh, parentless trace, and a slow or failing request couldn't be followed across the proxy. DC-341 closes that gap: OpenTelemetry for the web tier, exported to the same collector the backend uses.

## Decision

Instrument the Next.js apps with the raw OpenTelemetry JS SDK, packaged as a shared `@sebt/observability` workspace package that both apps register through Next's `instrumentation.ts` hook.

| Choice | Decision | Why |
|--------|----------|-----|
| SDK | Raw `@opentelemetry/sdk-node` | Matches the backend; no `@vercel/otel` wrapper lock-in |
| Packaging | Shared `@sebt/observability` package | One bootstrap for both Next apps; server-only, so it stays out of client bundles |
| Signals | Traces + metrics + logs pipeline | Logs pipeline is wired but *dark* until a pino retrofit gives it an emit source |
| Protocol | gRPC default, switchable via `OTEL_EXPORTER_OTLP_PROTOCOL` | gRPC matches the backend (`:4317`); http/protobuf available for HTTP-only paths |
| Proxy tracing | Manual `http.proxy` span in the API proxy route | Adds proxy-level error semantics (timeout → 504, backend down → 502) as span status/exceptions |

The SDK is *inert when unconfigured*: with no `OTEL_EXPORTER_OTLP_ENDPOINT`, `startOtel` resolves every signal to `none` and never starts. An environment that hasn't been pointed at a collector pays nothing.

## The connected trace

`UndiciInstrumentation` is what connects the two services' traces. It patches Node's `fetch` and injects the W3C `traceparent` header on outbound calls. The proxy route's `fetch` to the backend therefore carries the active trace context, and the .NET backend (already propagating W3C TraceContext) continues the *same* trace as a child. No explicit correlation code.

The manual `http.proxy` span wraps the proxy operation so its failure modes are visible — `UndiciInstrumentation` alone traces the raw HTTP call but doesn't know the proxy maps a timeout to 504 or an unreachable backend to 502.

## Deployment — matches the backend exactly

The app always emits OTLP to `localhost:4317`. A collector sits there and routes onward. The endpoint is a fixed convention, never a per-environment variable — same as the backend, which hardcodes `Otel:Otlp:Endpoint` in `appsettings.json` and overrides it nowhere.

- **Local:** the Jaeger container in `compose.yaml`.
- **ECS (DC dev, CO):** an ADOT collector sidecar on the web task (`module.web` in `tofu/modules/sebt_application/`), mirroring the API. It forwards traces to Datadog APM when the Datadog integration key is present, reusing the existing `otel-config.yaml.tftpl` unchanged.
- **IIS (DC prod):** OTEL vars in the `web.config` template. These are runtime vars, so `web.config`'s `environmentVariables` block is correct — unlike `NEXT_PUBLIC_*`, which is build-time inlined.

## Alternatives considered

1. **`@vercel/otel`** — the Next-recommended wrapper.
   🟡 Fastest for traces, but thinner metrics/logs support and an opinionated surface. Diverges from the backend's raw-SDK pattern.
2. **Per-app telemetry module** (no shared package) — instrument each app in-place.
   🟡 Simpler wiring, but the identical bootstrap drifts across two apps over time. A shared package is the same pattern already used for `@sebt/design-system` and `@sebt/analytics`.
3. **External-endpoint model** — point the app at a collector URL via a deploy-time variable.
   🔴 Not how the backend works. It would introduce a second, inconsistent telemetry-routing pattern and require a reachable collector to exist independently of the task.

## Consequences

- **Logs export is dark until a pino retrofit.** The pipeline exists, but nothing emits OTEL log records yet; `console.*` doesn't auto-convert. Traces and metrics are live; span-recorded exceptions already carry error detail. Tracked as a follow-up.
- **The enrollment checker's server OTEL is inert in production.** It deploys as a static export (S3/CloudFront) with no Node server, so `register` only runs at build time as a no-op. Instrumentation is kept to stay SSR-ready; real telemetry there would need browser RUM (out of scope).
- **Where signals land is collector config, not app code.** The apps emit all three signals as OTLP to the sidecar; routing is `otel-config.yaml.tftpl`'s job — Datadog for dev/CO, Splunk for DC (the in-flight telemetry work). Today the collector sends traces to Datadog APM and metrics to CloudWatch, with no logs pipeline; extending it to route metrics and logs onward is that collector work, not a change here.
- Adding a new signal or instrumentation is a one-file change in `@sebt/observability`, inherited by both apps.

## References

- [DC-341](https://codeforamerica.atlassian.net/browse/DC-341) — this work
- `SEBT.Portal.Api/Telemetry/` — the backend OpenTelemetry setup this mirrors
- Numbered 0018 because `0017` is the merged monorepo-consolidation ADR.
