# 0017 — OpenTelemetry Distributed Tracing

**Status:** Accepted  
**Date:** 2026-06-02

## Context

The portal is two services: a Next.js Node.js frontend and a .NET 10 API. Requests originate in the browser, proxy through Next.js, and land in .NET. Without distributed tracing, a slow or broken request is hard to locate — Jaeger shows the .NET side, but the Next.js hop is invisible.

The .NET backend already instruments with the OTel SDK (`sebt-portal-api`). We needed parity on the Node.js side.

## Decision

Instrument the Next.js app with the [OpenTelemetry JS SDK](https://opentelemetry.io/docs/languages/js/) directly — not `@vercel/otel` or any wrapper. Mirrors the backend pattern exactly: raw SDK, same env vars, same defaults.

**Key choices:**

| Choice | Decision | Why |
|--------|----------|-----|
| SDK | Raw `@opentelemetry/sdk-node` | Matches backend; no wrapper lock-in |
| Protocol default | gRPC (`OTEL_EXPORTER_OTLP_PROTOCOL=grpc`) | Matches backend default; port 4317 |
| Both gRPC + HTTP | Supported; switch via env var | Flexibility without code changes |
| Signals | Traces ✓ Metrics ✓ Logs ✗ (off by default) | Logs off matches backend's "planned later" posture |
| Runtime guard | `NEXT_RUNTIME === 'nodejs'` in `instrumentation.ts` | OTel Node.js SDK uses `async_hooks` — incompatible with Next.js edge runtime |
| Manual span | `http.proxy` in `proxyRequest()` | Single choke point for all backend traffic |

## The Connected Trace

The `http.proxy` span wraps the `fetch()` call in `route.ts`. `context.with(trace.setSpan(...), fn)` makes the span active during the fetch. `UndiciInstrumentation` (which traces Node.js native `fetch`) reads the active context and injects a W3C `traceparent` header into the outgoing request automatically. The .NET backend (already tracing with W3C TraceContext propagation) creates a child span under that parent.

Result: Jaeger shows one connected trace — `http.proxy` in `sebt-portal-web` as the root, `.NET` controller/handler spans as children. No explicit correlation code needed.

## Deployment

Both containers (Node.js and .NET) read the standard `OTEL_*` env vars. A single pair of GitHub Variables (`OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL`) configures both via `TF_VAR_otel_*` in `deploy-ecr.yaml`. Production target is Datadog's OTLP endpoint.

## Consequences

- All requests through the proxy produce an `http.proxy` span with method, path, and response status code.
- Traces are off by default in any environment where `OTEL_EXPORTER_OTLP_ENDPOINT` is unset (the SDK starts but exports to nowhere).
- Disabling signals: set `OTEL_TRACES_EXPORTER=none` or `OTEL_METRICS_EXPORTER=none`.
- EnrollmentChecker.Web is out of scope; it can adopt the same pattern independently.
