# Identity Assurance Level (IAL) Configuration

This document describes how to configure identity proofing requirements for the
SEBT Self-Service Portal. These settings control what users can see and do based
on how thoroughly they've verified their identity.

For architectural context, see [ADR-0012](../../adr/0012-unified-id-proofing-requirements.md).

## Overview

The portal uses a **resource+action** access control model. Each combination of
a protected resource and an action has a configurable IAL requirement:

| Key              | What it controls                                      |
| ---------------- | ----------------------------------------------------- |
| `address+view`   | Can the user see their address on file?               |
| `address+write`  | Can the user change their address?                    |
| `email+view`     | Can the user see their email on file?                 |
| `phone+view`     | Can the user see their phone number on file?          |
| `household+view` | Can the user access their household/case data at all? |
| `card+write`     | Can the user request a replacement EBT card?          |

### IAL levels

| Level      | Meaning                                                                         |
| ---------- | ------------------------------------------------------------------------------- |
| `IAL1`     | Basic login completed (email/phone OTP). No document verification.              |
| `IAL1plus` | Enhanced verification completed (e.g., Socure check via MyCO).                  |
| `IAL2`     | Reserved for future use. Not currently achievable via in-app step up for users. |

### How enforcement works

1. When a user logs in, their IAL level is recorded in their JWT (`ial` claim).
2. On each request, the backend checks the user's IAL against the configured
   requirement for the resource+action they're attempting.
3. If the user's IAL is below the requirement, the request is denied with a
   403 response that includes the required level.
4. The frontend uses the 403 response to redirect the user to step-up
   identity verification (if configured for the state).

## Defaults

Every requirement defaults to **IAL1plus** if not explicitly configured. This is
a deliberate security choice: missing configuration results in the most
restrictive behavior, not the most permissive.

The base `appsettings.json` (checked into source control) contains:

```json
"IdProofingRequirements": {
  "address+view": "IAL1plus",
  "address+write": "IAL1plus",
  "email+view": "IAL1",
  "phone+view": "IAL1",
  "household+view": "IAL1plus",
  "card+write": "IAL1plus"
}
```

These defaults apply unless overridden by a state-specific configuration source.

## Configuration sources (priority order)

Later sources override earlier ones:

1. **`appsettings.json`** — base defaults (checked into source control)
2. **Environment variables** — set via Tofu/ECS task definition
3. **AWS AppConfig** — highest priority, supports hot reload without redeploy

## Simple vs. granular configuration

### Simple form (uniform for all case types)

When all case types have the same IAL requirement for an action, use a simple
string value:

```json
"address+write": "IAL1plus"
```

This means every user needs IAL1plus to change their address, regardless of how
their cases were loaded.

### Granular form (per-case-type)

When different case types need different IAL levels, use an object:

```json
"household+view": {
  "application": "IAL1",
  "coloadedStreamline": "IAL1",
  "streamline": "IAL1plus"
}
```

The case type keys are:

| Key                          | Meaning                                                                       |
| ---------------------------- | ----------------------------------------------------------------------------- |
| `application`           | Cases from guardian-submitted applications                                    |
| `coloadedStreamline`    | Streamline-certified cases bulk-imported from state systems (e.g., SNAP/TANF) |
| `streamline` | Streamline-certified cases not bulk-imported (added through the portal)       |

**"Highest wins" rule:** When a user has multiple cases of different types, the
highest required IAL across all their cases applies. For example, if a user has
both a co-loaded case (IAL1) and a non-co-loaded case (IAL1plus), they must meet
IAL1plus.

## Validation

The application validates IAL configuration at startup and on every config
reload. It will **refuse to start** (or reject a config change) if:

1. **Write < View for the same resource.** For example, `address+write: IAL1`
   with `address+view: IAL1plus` is rejected. You should not be able to change
   data that you can't see.
2. **Step-up OIDC configured but no write operation requires > IAL1.** If the
   state has configured step-up authentication (MyCO, etc.), at least one write
   requirement must be above IAL1. Otherwise the step-up infrastructure is
   configured but never triggered.

Unrecognized keys (typos like `"adress+view"`) are logged as warnings but do
not prevent startup.

## State configurations

### DC (District of Columbia)

DC uses email/DOB login and does not have OIDC step-up authentication. IAL
requirements vary by case type for household data access.

```json
"IdProofingRequirements": {
  "address+view": "IAL1plus",
  "address+write": "IAL1plus",
  "email+view": "IAL1",
  "phone+view": "IAL1",
  "household+view": {
    "application": "IAL1",
    "coloadedStreamline": "IAL1",
    "streamline": "IAL1plus"
  },
  "card+write": "IAL1plus"
}
```

DC does not currently set these via Tofu — they come from the appsettings
overlay or AppConfig.

### CO (Colorado)

CO uses PingOne OIDC with Socure step-up for identity verification. All case
types currently have the same IAL requirement (CO does not co-load yet; co-loading
is expected in 2027).

```json
"IdProofingRequirements": {
  "address+view": "IAL1plus",
  "address+write": "IAL1plus",
  "email+view": "IAL1plus",
  "phone+view": "IAL1",
  "household+view": "IAL1plus",
  "card+write": "IAL1plus"
}
```

#### Tofu environment variables (CO)

CO sets these via `state_api_environment_variables` in Tofu. The .NET
configuration system uses `__` (double underscore) as the section separator.

```hcl
state_api_environment_variables = {
  # Simple form: "section__key" = "value"
  "IdProofingRequirements__address+view"      = "IAL1plus"
  "IdProofingRequirements__address+write"      = "IAL1plus"
  "IdProofingRequirements__email+view"         = "IAL1plus"
  "IdProofingRequirements__phone+view"         = "IAL1"
  "IdProofingRequirements__household+view"     = "IAL1plus"
  "IdProofingRequirements__card+write"         = "IAL1plus"
}
```

If CO later needs per-case-type granularity (e.g., when co-loading begins in
2027), the granular form uses an additional `__` level:

```hcl
state_api_environment_variables = {
  # Granular form: "section__key__caseType" = "value"
  "IdProofingRequirements__household+view__application"            = "IAL1"
  "IdProofingRequirements__household+view__coloadedStreamline"     = "IAL1"
  "IdProofingRequirements__household+view__streamline"  = "IAL1plus"
}
```

Note: when using the granular form for a key, do **not** also set the simple
form for that same key. The simple form value would be ignored because the .NET
configuration system treats the key as a section (with children) rather than a
leaf value.

## Migrating from the old MinimumIal configuration

If your deployment previously used `MinimumIal__*` environment variables:

| Old key                                  | New key                                                                                                                                                           |
| ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MinimumIal__application`           | `IdProofingRequirements__household+view__application` (granular) or `IdProofingRequirements__household+view` (simple, if all case types have the same value) |
| `MinimumIal__coloadedStreamline`    | `IdProofingRequirements__household+view__coloadedStreamline`                                                                                                 |
| `MinimumIal__streamline` | `IdProofingRequirements__household+view__streamline`                                                                                              |

The `MinimumIal` configuration section is no longer read. Remove old keys after
deploying the update to avoid confusion.
