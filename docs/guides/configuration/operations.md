---
description: Rate limits, caching, telemetry, and the settings that protect data at rest.
keywords: rate limit throttling Redis cache OpenTelemetry Otel PII encryption hashing operations
---

# Operations

## Rate limits

Four endpoints are limited independently, so tightening one does not throttle the others.

| Section | Protects |
| --- | --- |
| `OtpRateLimitSettings` | Requesting and validating one-time passcodes |
| `EnrollmentCheckRateLimitSettings` | The unauthenticated enrollment check |
| `CheckerFeaturesRateLimitSettings` | The feature endpoint the checker polls |
| `WebhookRateLimitSettings` | The inbound document-verification webhook |

Each takes the same two fields. `OtpRateLimitSettings` defaults to:

| Field | Default | Meaning |
| --- | --- | --- |
| `PermitLimit` | `5` | Requests allowed per window |
| `WindowMinutes` | `1.0` | Window length in minutes |

The two unauthenticated endpoints, the enrollment check and the checker feature poll, are the ones exposed to the
open internet and the most likely to need tuning. The checker polls on a timer, so a limit set too low throttles
ordinary visitors rather than abuse.

Limits are applied per client IP. In production the API sits behind the Next.js server, which forwards the real
client address; without that every request would appear to come from one address and share a single bucket.

## Caching

| Section | Purpose |
| --- | --- |
| `Redis` | Connection details for the shared cache |

Redis backs caching and distributed locking. With it disabled the lock provider falls back to the database, which
works but puts lock traffic on the same connection as everything else.

## Telemetry

| Section | Purpose |
| --- | --- |
| `Otel` | OpenTelemetry exporter endpoints and sampling |

Traces, metrics, and logs go out over OTLP. Logging itself is configured separately through the `Serilog` and
`Logging` sections.

## Data protection

| Section | Purpose |
| --- | --- |
| `PiiEncryption` | Keys for encrypting personal data at rest |
| `IdentifierHasher` | The secret key behind the deterministic hashes used for lookups |

These two are not interchangeable. Encryption is reversible and is for data that must be read back. Hashing is
one-way, for values needed only to match against, such as cooldown checks and deduplication.

`IdentifierHasher` normalizes before hashing, trimming whitespace and stripping dashes and spaces, so `SEBT-001` and
`SEBT001` produce the same hash. Read and write paths must both go through it or lookups silently miss.

Rotating `IdentifierHasher:SecretKey` invalidates every stored hash, because the old values can no longer be
reproduced. There is no way to recover the originals from the hashes.
