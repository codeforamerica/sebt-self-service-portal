---
description: How the portal protects the data a state trusts it with, what is encrypted and where, and the gaps we have not closed.
keywords: security privacy encryption KMS AES-GCM PII at rest in transit session timeout NIST 800-63B OWASP identity assurance IAL authorization data boundary secrets rotation CSP rate limiting CodeQL Dependabot threat model penetration test SBOM
---

# Security and privacy

A state adopting this portal is trusting it with a path into a system of record that holds benefit data for
children. This page says what protects that path, where each control is implemented, and what is not yet covered.
Everything below is drawn from the code and infrastructure in this repository, and each claim names where it lives so
it can be checked rather than taken on faith.

## What the portal holds

The single most useful fact for a security review is how little there is to protect. The portal's own database has
six tables, and none of them hold household data, case numbers, benefit amounts, or card numbers. Those are read from
the state's systems for the request that needs them and are not written down.

The full table list, and what each one holds, is in [Overview](../docs/overview/index.md). It is worth
reading before this page, because most of the controls below exist to protect a deliberately small surface.

## Encryption

There are two independent layers. Either one alone would protect data at rest; a state gets both, and can decide
whether to turn the second one on.

### Infrastructure

Provisioned by the OpenTofu modules in `tofu/modules/`, so it is applied by configuration rather than by remembering
to do it.

| What | Control |
| --- | --- |
| Database | A dedicated KMS key, with `enable_key_rotation = true`. |
| Cache | A dedicated KMS key for Redis, also with rotation enabled. |
| Cache authentication | The Redis AUTH token is held in Secrets Manager and encrypted with that key. |
| Cache traffic | TLS in transit, which local development mirrors so the encrypted path is the one being tested. |

### Application

Above that, individual PII columns can be encrypted by the application before they reach the database, so the
database never sees plaintext. The design is recorded in
[ADR 0015](../adr/0015-pii-encryption-at-rest.md).

- **AES-256-GCM**, with a random nonce for every encryption.
- A versioned envelope prefixed `sep-pii:v1:` carrying the key id, nonce, and authentication tag, so ciphertext
  written under an older key stays readable and a key ring can be rotated without a schema change.
- Decryption failures raise `PiiDecryptException` rather than returning an empty string, so corruption or tampering
  is loud instead of silent.
- Where a value has to be searchable, it is stored twice: as ciphertext, and as a lookup hash. Queries match on the
  hash, so the plaintext is never needed to find a row.

**This layer is a state's choice.** `PiiEncryption:EncryptAtRest` defaults to off. Colorado leaves it off and relies
on the infrastructure layer; DC turns it on and runs a backfill to encrypt existing rows. When it is on in
production, a guard rejects placeholder keys at startup rather than letting a deployment come up with a key that
protects nothing.

Separately, identifiers that exist only so the portal can look something up, such as the household and case
identifiers behind a card replacement cooldown, are stored as HMAC-SHA256 hashes and never in the clear. Those are
one-way by intent: nothing in the portal reads the original value back.

## Identity, assurance, and sessions

**Assurance.** How much of a household's data a user may see depends on the identity assurance level they have
reached. Which level is required for what is recorded in
[ADR 0027](../adr/0027-unified-id-proofing-requirements.md). Whether the portal performs the proofing itself or
consumes an assurance level asserted by the state's own SSO is a per-state decision, described in
[Overview](../docs/overview/index.md).

**Sessions.** Two timeouts run together, explicitly aligned to
[OWASP Session Management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html) and
[NIST SP 800-63B §7.1](https://pages.nist.gov/800-63-3/sp800-63b.html), whose IAL2 guidance is 30 minutes idle and 12
hours absolute. The defaults here are stricter than both.

| Timeout | Default | How it works |
| --- | --- | --- |
| Idle | 15 minutes | The session refreshes only if there has been real user activity. An idle tab stops refreshing and the session lapses. |
| Absolute | 60 minutes | Stamped once at first login as the standard OIDC `auth_time` claim and never re-stamped, so refreshing cannot extend a session past the cap. |

A configuration validator rejects a setup where the absolute cap is shorter than the idle window. The reasoning is in
[ADR 0014](../adr/0014-session-idle-and-absolute-timeout.md).

## Authorization at the data boundary

Access is enforced at the API endpoint that returns the data, not in the interface. Client-side checks are treated as
conveniences, not controls.

When an authenticated user lacks sufficient assurance for the household they are asking about, the API returns
**403 with structured problem details** naming the required level, rather than a 200 carrying filtered or empty data.
A denial the client can act on is more useful than a silent one, and it does not leak whether the data exists.

These checks re-evaluate on every request rather than trusting what a token asserts. A JWT issued before a
household's composition changed is stale, and a control that believed it would be wrong.

## Secrets and credential rotation

Secrets are loaded from environment variables or Docker secret files and are never committed. State configuration
overlays that carry credentials are kept out of git, with only `.example` variants in the repository.

Database credentials rotate automatically using an **alternating-users** strategy, implemented as a Lambda deployed
with the database module. The consequence worth noting for a reviewer: the application reads a dedicated app-user
secret and never holds the master credential. The master exists only so the rotation Lambda can manage the app user's
logins. The alternatives that were considered are in
[ADR 0018](../adr/0018-db-credential-rotation-strategy.md).

## The browser boundary

**The API is not addressable from a browser.** Every request to `/api/...` goes to the Next.js server first, through
a single catch-all route. That route rejects path traversal, literal or URL-encoded, so a crafted request cannot
escape `/api/` and reach the backend's own `/health` or `/swagger`. The OIDC token exchange happens server-side in
.NET, so the `code_verifier` and client secret are never sent to a browser.

**Content Security Policy** is set per request with a fresh nonce. The policy pins `default-src 'self'`,
`frame-ancestors 'none'`, `object-src 'none'`, and `base-uri 'self'`, with `script-src`, `style-src`, and
`connect-src` narrowed to the origins actually needed.

Because the policy is an allowlist, adding any browser-side call to a new external domain requires a matching
directive entry. This is easy to miss: CSP is not enforced in local development or in tests, so a missing entry
surfaces only once deployed, where it looks like the third-party service being down rather than a policy violation.

## Abuse resistance and supply chain

**Rate limiting** is applied per endpoint class, with named policies for one-time passcodes, enrollment checks,
checker features, and webhooks. Passcode requests carry an additional limit keyed on the email address rather than
just the caller's network address, so rotating IPs does not reset the limit. Where an address is recorded for an
enrollment check, it is stored as a hash.

**Diagnostic and bypass flags stay server-side.** The one-time passcode bypass and the test error endpoints are gated
through the feature manager, and are deliberately excluded from the anonymous feature payload the browser receives,
so their state is not advertised to an unauthenticated caller.

**Supply chain** scanning runs in CI: CodeQL across four languages (`actions`, `csharp`, `javascript-typescript`,
`python`) on every push and pull request to `main` plus a weekly schedule, and Dependabot across six package
ecosystems.

## Gaps

Stated plainly, because a security review will find them anyway and it is better that they are listed than
discovered.

- **No threat model is documented.** The controls above are individually reasoned, several of them in ADRs, but there
  is no single document tracing assets, actors, and attack paths.
- **No penetration test or third-party assessment is published here.** If one has been performed for a given state
  deployment, it lives with that state rather than in this repository.
- **No SBOM is generated.** Dependencies are tracked and patched through Dependabot, but no signed bill of materials
  is produced for a release.
- **No secret scanning runs in CI.** The rule against committing secrets is enforced by review and convention rather
  than by a tool that would catch a mistake.
- **No DAST runs in this repository.** The one-time passcode bypass exists to support dynamic scanning, but the
  scanning itself happens outside these workflows.
- **`style-src` allows `'unsafe-inline'`.** Socure's document verification SDK requires it. The compensating controls
  are `frame-ancestors 'none'`, no use of `dangerouslySetInnerHTML`, and a nonce on scripts, but the style directive
  is genuinely weaker than the rest of the policy. That SDK also sends telemetry to a third-party endpoint, which
  `connect-src` must allow.
- **Application-layer PII encryption is off by default**, and off in Colorado. Infrastructure encryption still
  applies, so data is encrypted at rest either way, but the database sees plaintext in that configuration.
- **`security-checks.yaml` is not a security scanner**, despite the name. It checks that pnpm cooldown exclusions are
  not stale. The actual scanning is CodeQL and Dependabot.

## Related

- [Compliance](index.md) covers the four obligation areas and what is checked automatically.
- [Licensing](licensing.md) covers the license and the dependency policy.
- [Overview](../docs/overview/index.md) describes the runtime shape these controls protect.
