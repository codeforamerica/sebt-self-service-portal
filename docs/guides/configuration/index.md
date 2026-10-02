---
description: How the portal is configured, the four layers settings merge through, and where each group of features is documented.
keywords: configuration config feature flags settings appsettings AppConfig environment variables state overlay precedence
---

# Configure features and settings

The portal is customized in two ways. **Feature flags** are booleans that turn behaviour on and off and can be
changed at runtime. **Configuration sections** hold structured values such as endpoints, thresholds, and keys, and
are set at deploy time.

This section documents both, grouped by what they control.

| Page | Covers |
| --- | --- |
| [Season and applications](season-and-applications.md) | Whether the season is enrolling and whether applications are open |
| [Outages and maintenance](outages.md) | Taking either app down, on a schedule or immediately |
| [What households see and can do](household-view.md) | Dashboard fields, and which self-service actions are allowed |
| [Enrollment checker](enrollment-checker.md) | The standalone checker, including income screening |
| [Identity and proofing](identity-and-proofing.md) | Sign-in, session tokens, and identity assurance levels |
| [Addresses and messaging](addresses-and-messaging.md) | Address validation and outbound email |
| [Operations](operations.md) | Rate limits, caching, telemetry, and data protection |
| [Non-production settings](non-production.md) | Settings that must never be enabled in production |

## The four layers

Settings merge in this order, each overriding the one above it.

| Layer | Holds | Changing it needs |
| --- | --- | --- |
| `appsettings.json` | Defaults shared by every state | A redeploy |
| `appsettings.{STATE}.json` | One state's overlay, selected by the `STATE` environment variable | A redeploy |
| AWS AppConfig | Runtime overrides, highest priority | Nothing; both apps pick it up without a redeploy |
| Environment variables | Connection strings, secrets, anything not committed | A restart |

Only `.example.json` overlays are in the repository. The real `appsettings.co.json` and `appsettings.dc.json` are
supplied by the deployment, so the examples show the shape of an overlay rather than what a state is running.

`NEXT_PUBLIC_*` variables are a separate case. Next.js inlines them into the browser bundle when `pnpm build` runs,
so setting one on an already-built server has no effect. See [Build the portal](../build/index.md).

## How flags behave

Flags are read through `IFeatureManager`. The `/api/features` endpoint returns only flags that are explicitly
configured somewhere, so an unset flag is absent from the response rather than false. Two flags are never returned
at all; see [Non-production settings](non-production.md).

Flag names may contain only letters, digits, and underscores, which is what AWS AppConfig accepts. A name that
breaks that rule is skipped with a warning rather than applied.

The display flags are read by name in the front-end code rather than through the `FeatureFlags` constants class, so
a misspelled flag name fails quietly instead of failing to compile.

## Where other customization lives

| To change | See |
| --- | --- |
| Wording in either app | [Change user-facing text](../content/index.md) |
| Colors, fonts, logos | [Change how the portal looks](../branding/index.md) |
| Which state backend is used | [Build a state connector](../state-connector/index.md) |
