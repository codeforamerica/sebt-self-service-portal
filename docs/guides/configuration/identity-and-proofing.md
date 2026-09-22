---
description: Sign-in, session tokens, and the identity assurance levels required to see household data.
keywords: OIDC identity proofing IAL step-up JWT session Socure document verification assurance
---

# Identity and proofing

Two related concerns: how someone signs in, and how much identity assurance they need before the portal will show
them a household.

Changing anything here changes who can see benefit data. Treat it as a security change rather than a configuration
tweak.

## Sign-in

| Section | Purpose |
| --- | --- |
| `Oidc` | The state's identity provider: discovery endpoint, client id, redirect URIs |
| `Oidc:StepUp` | A second, higher-assurance provider used when an action needs more than the session has |
| `Oidc:VerificationClaims` | Which claims from the provider count as verification |
| `JwtSettings` | Signing and lifetime for the portal's own session token |

Step-up is configured separately because it is a distinct provider interaction, not a re-run of the first one. A
state with no step-up path leaves the section empty.

## Identity assurance levels

`IdProofingRequirements` holds a `Requirements` map from action and cohort to the assurance level that action
demands. Keys are read case-insensitively.

This is enforced at the data boundary, in the API that returns the data, not in the interface. A household that
falls short receives a 403 carrying the level it would need, rather than an empty result that looks like having no
benefits. Lowering a requirement here widens who can read household data.

## How long proofing lasts

| Section | Field | Default | Meaning |
| --- | --- | --- | --- |
| `IdProofingValidity` | `ValidityDays` | `1826` | How long a completed proofing stays good |

1826 days is five years including a leap day. After it lapses a household must prove identity again.

## Document verification

| Section | Field | Meaning |
| --- | --- | --- |
| `IdProofingEligibility` | `RequireQualifyingHouseholdForSocure` | Whether a household must qualify before a document-verification session may start |
| `Socure` | | Credentials and endpoints for the document-verification vendor |
| `Socure` | `DocvEgregiousReasonRejection` | `Enabled`, plus the `ReasonCodes` treated as terminal rather than retryable |

`RequireQualifyingHouseholdForSocure` gates a paid, per-session vendor call. Leaving it off means sessions can be
started by people with no matching household.

The vendor reports back over a webhook, which is rate-limited separately. See [Operations](operations.md).

## Session claims go stale

A token's claims describe the household as it stood at sign-in. Composition can change afterwards, so the checks
that matter re-evaluate server-side on each request rather than trusting the token. Anything added here should do
the same.
