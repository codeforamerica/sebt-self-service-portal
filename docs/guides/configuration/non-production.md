---
description: Settings that exist for development, testing, and scanning, and must never be enabled in production.
keywords: development testing mock data seeding bypass OTP diagnostic endpoints DAST non-production
---

# Non-production settings

Everything on this page weakens a control deliberately. Each is safe in development and dangerous in production.

## Flags that are never advertised

| Flag | Default | Controls |
| --- | --- | --- |
| `test_error_endpoints_enabled` | `false` | The diagnostic endpoints under `/api/test-error` |
| `bypass_otp` | `false` | Skipping one-time passcode validation for the security scanner account |

Unlike every other flag, these two are excluded from the `/api/features` response. Reporting their state to an
anonymous caller would itself disclose whether a bypass is available, so they are checked server-side only.

Their absence from that endpoint means you cannot confirm they are off by reading it. Check the deployed
configuration instead.

`bypass_otp` turns off the second factor for a named account so the automated security scanner can reach
authenticated pages. On in production, it is an authentication bypass.

The diagnostic endpoints return chosen status codes and raise deliberate exceptions to exercise error handling. They
are documented in the [REST API reference](../../rest/index.md) under Diagnostics, because they are real routes,
but they are gated behind the flag and off by default.

## Mock and seeded data

| Setting | Purpose |
| --- | --- |
| `UseMockHouseholdData` | Serves households from an in-memory repository instead of calling a state connector |
| `Seeding` | Controls the development data seeder |
| `DevelopmentPhoneOverride` | Redirects outbound messages to a fixed number |

`UseMockHouseholdData` affects reads **and** writes. With it on, an address update or card replacement appears to
succeed while reaching nothing, which makes it useful for interface work and misleading for testing a connector.
Test personas are keyed by email and phone.

## Local identity provider

A local OIDC stand-in is available so development does not need the real state provider. It is configured through
the same `Oidc` section, pointed at the local instance. See
[Set up your environment](../local-setup/index.md).

## Before a production deploy

Confirm each of these is off or absent:

- `test_error_endpoints_enabled`
- `bypass_otp`
- `UseMockHouseholdData`
- `DevelopmentPhoneOverride`
- Seeding, other than any migration the environment genuinely needs

The first two will not show up in `/api/features`, so read the configuration rather than the endpoint.
