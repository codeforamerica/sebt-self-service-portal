# 24. Adopt a translation management system for portal copy

Date: 2026-08-19

## Status

Proposed. Amends [0006-i18n-implementation.md](./0006-i18n-implementation.md): the authoring and publishing workflow changes. The generated JSON delivery stays.

## Context

Authors write portal and enrollment-checker copy in a Google Sheet. Engineers export it by hand to per-state CSVs, and the build compiles those into locale JSON. This CSV pipeline has three problems:

- **It fails silently.** Duplicate keys resolve by last row wins, and unpublished copy ships. A spike validation pass found dozens of defects in the two live CSVs, including duplicate keys with differing values.
- **Every copy change is an engineering release.** DC production also needs a manual deployment that an administrator runs.
- **There is no translation workflow:** no review states, no reviewer roles, no quality checks.

A successor must provide:

- Translation review and content quality checks (weighted most heavily).
- Per-state variants of shared copy, per-app targeting, draft and publish states.
- Byte-for-byte markdown (0010-rich-text-rendering.md) and a $0 path with no vendor lock-in.
- All three deployment targets, including DC on-prem IIS with no assumed outbound egress.

The design and content team and the state partners also need:

- Flexible content regions instead of a fixed number of keys per page, and rich text with links and embedded components.
- A graphical tool that non-technical editors can use after handoff.
- A way to send copy to outside translators and bring it back. Amharic is translated outside engineering.
- A side-by-side view of copy across states and languages, which the sheet gives today.
- A way to add a language after handoff.
- A manual, backwards-compatible path for a state with neither a git flow nor a container platform.
- Publishing without an engineering deploy.

Needs that this decision does not yet meet appear under "What to look out for next".

Constraints:

- Repository dependencies must satisfy the license allowlist. The platform license is PolyForm Noncommercial 1.0.0.
- This ADR assumes that a separately hosted service whose output we consume does not implicate the platform's license. A copyleft library in the repository would. Whether a state can be asked to host GPL software is part of the policy sign-off.
- DC's engineering contact reports that DC objects to Docker over licensing, so hosting must not depend on Docker.

## Decision

We adopt **Weblate (self-hosted)** for portal and enrollment-checker copy. Adoption requires a policy sign-off that GPL-3.0 is acceptable for server-only infrastructure that contributes no code to this repository.

- **Content contract:** a translation unit stays a key to a markdown string, by language and state. Values may also use an allowlisted set of component tags.
- **Delivery:** Weblate commits reviewed, publish-gated i18next JSON to the git repository. The build pipeline does not change, and no Weblate library enters the codebase.
- **CSV pipeline:** we keep it supported. Retiring it is a separate call with the design and content team.
- **Hosting:** Weblate runs as its own container stack beside the portal. Podman is the default container runtime and Docker is the fallback. The spike proved the full loop under rootless Podman on a developer Mac.
- **Sequence:** the companion ADR (validate and publish-gate the CSV locale pipeline) goes first. Its clean validation run is the prerequisite for the migration import.
- **Fallback:** if the sign-off is refused, we adopt **Payload** (self-hosted, MIT). Its i18next export and review workflow would be built, not configured. **Locize** (paid SaaS) needs explicit budget approval.

## Alternatives considered

The evaluation scanned thirteen candidates in four categories: SaaS CMS, self-hosted CMS, git-based CMS, and translation management systems. It verified licenses and prices against vendor sources, then scored four finalists (Weblate, Locize, Payload, Sanity) against two baselines.

- **Locize:** paid SaaS. A state cannot be asked to inherit a subscription.
- **Contentful:** lower tiers cap locales, so our six locales need Enterprise pricing.
- **Ditto:** paid, with no published pricing.
- **Payload:** MIT and $0, but the review workflow and i18next export are custom work, and it adds an admin login inside the portal boundary. It is the fallback.
- **Sanity:** free nonprofit plan, but localization and review are roll-your-own, and content is vendor-hosted in the EU.
- **Strapi, Tolgee:** the features we need are paid even when self-hosted.
- **Traduora** (AGPL-3.0, dormant) and **Directus** (source-available license): fail the license allowlist.
- **Decap, TinaCMS, Keystatic:** every change stays on the commit-and-release cycle.
- **Bespoke build:** exceeds the budget and adds an admin login inside the portal boundary.
- **Status quo with validation only:** leaves authoring, review, and publish latency unchanged.

## Consequences

Positive:

- Content authors edit, review, and approve copy without engineering.
- Built-in i18next interpolation checks remove a bug class that has shipped to production.
- Copy stays in the git repository: no vendor lock-in, simple rollback, and $0 licensing, container runtime included.

Trade-offs:

- Propagation keeps shared copy in sync, but it is all-or-nothing per component. An edit in one state overwrites the other state's differing translation (observed in the spike).
- We operate and patch the service. Podman is our tested path, not the vendor's documented one, and a real Linux server is untested.
- There is no rendered preview. Copy freshness is publish, automated commit, then CI deploy.
- GPL-3.0 needs an explicit policy sign-off, recorded with this ADR's acceptance.
- Weblate is a new administrative surface. It holds public copy only (no PII), and the SEBT threat model must document it.

What to look out for next:

- **Hosting:** the spike proved the path on a developer machine only. A real server is the next proof.
- **Translators:** the bulk export and import round trip needs a hands-on proof.
- **States after handoff:** what each state can host, who owns its content, and the path for a state with no container platform or git flow.
- **Shared copy:** an edit in one state can overwrite the other state's translation.
- **Design team:** the component-tag allowlist and the Figma data source are not agreed yet.

## References

- SEBT-662, the CMS evaluation spike. Branch: `spike/SEBT-662-cms-evaluation`
- SPADE: content pipeline for the SEBT self-service portal (the decision record for this evaluation)
- Weblate: https://docs.weblate.org
- Podman: https://podman.io and https://docs.podman.io/en/latest/markdown/podman-systemd.unit.5.html
- Docker Desktop license terms: https://docs.docker.com/subscription/desktop-license/
