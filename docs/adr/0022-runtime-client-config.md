# 22. Serve browser-facing config at request time instead of inlining it

Date: 2026-09-01

## Status

Accepted for the portal's vendor and analytics configuration. `NEXT_PUBLIC_STATE` is explicitly out of scope; see "Where we stop".

## Context

Next.js replaces every `process.env.NEXT_PUBLIC_*` reference with a literal during `next build`. The value is therefore fixed by whichever environment ran the build. Setting the same variable later — on the running container, or in the IIS `web.config` `<environmentVariables>` block — cannot change what the browser receives, because the browser is reading a string that was already written into a static chunk.

The portal carried nine such values: Google Analytics, Amplitude, Mixpanel, SiteImprove, both Socure SDK keys, the Smarty embeddable key, and two development toggles. All nine were threaded through `--build-arg` in `deploy-ecr.yaml`, `ARG` lines in the Dockerfile, and a build-step `env:` block in `release-iis-dc.yaml`.

Two consequences followed. One artifact could not be promoted across environments, because each environment needed its own build — breaking [twelve-factor Config (III)](https://12factor.net/config) and [Build, release, run (V)](https://12factor.net/build-release-run). And the failure was silent: an unset build arg inlined an empty string, so the feature simply did nothing in the deployed environment with no error.

The middleware in `proxy.ts` made this sharper. It gates CSP `connect-src`/`script-src` entries on whether an analytics key is present. Because those reads were inlined too, a key supplied at runtime would have been blocked by a Content-Security-Policy frozen at build time — a second, less obvious way for the same misconfiguration to fail.

## Decision

- **Drop the `NEXT_PUBLIC_` prefix from browser-facing config.** Unprefixed names are never inlined, so they stay in the server process environment. `lib/runtime-config.ts` reads them and returns a typed `RuntimeConfig`.

- **Resolve per request in the root layout, hand off through context.** `layout.tsx` calls `getRuntimeConfig()` and wraps the tree in `RuntimeConfigProvider`; client components read it with `useRuntimeConfig()`. The values ride the RSC payload, so there is no extra round-trip and no render before config is known — analytics initializes on first paint rather than after a fetch settles.

  This costs nothing in prerendering: `generateMetadata` already awaits `headers()`, so every real route was already dynamically rendered. Only `_global-error` and `favicon.ico` are prerendered, and neither reads config.

- **`useRuntimeConfig()` returns an empty config outside the provider** rather than throwing, matching `useFeatureFlag()`. Every integration is optional, so "no config" is a legitimate state — an absent key already means "this integration is off" — and components stay renderable in isolation.

- **CSP is computed from the same unprefixed variables.** The middleware runs per request, so adding a key at release time widens the policy without a rebuild.

- **Config is no longer a build input.** The Dockerfile `ARG`s, the `--build-arg` flags in `deploy-ecr.yaml`, and the build-step `env:` entries in `release-iis-dc.yaml` are gone. Only build-identity values (`NEXT_PUBLIC_BUILD_SHA`, `NEXT_PUBLIC_DC_CONNECTOR_SHA`) stay inlined, which is correct — they describe the artifact, not the environment.

- **Where we stop: `NEXT_PUBLIC_STATE` keeps its prefix.** It is not merely a value; it selects build-time assets. `generate-tokens.js` writes `design/tokens.css` as an unscoped `:root {}`, `generate-sass-tokens.js` feeds `@use "uswds-core" with (...)` so USWDS compiles its whole utility set per state, and `design/fonts.ts` declares per-state `next/font/local` faces. Making state runtime-selectable means emitting every state's tokens, scoping them behind a `[data-state]` attribute, and loading both font sets — a CSS and font-loading change, not a config one. Tracked separately.

## Consequences

- One frontend artifact runs in any environment. Verified: an image built with no client config set served `G-RUNTIME-AAA`/`smarty-aaa` on one run and `G-RUNTIME-BBB`/`smarty-bbb`/`amp-bbb` on the next, with the CSP widening for Amplitude on the second run only.
- **Deployment coordination is required before this reaches production.** The values must now be set on the running container (Tofu / ECS task definition) for the Docker path, and in `web.config`'s `<environmentVariables>` on the host for the IIS path. Removing the build args without that leaves analytics, Socure, and Smarty switched off in deployed environments — the same silent failure as before, in the opposite direction.
- The `web.config` runtime env block is now load-bearing rather than inert. CLAUDE.md previously (and correctly) warned that it had no effect on browser code; that guidance is superseded for these variables.
- Tests that need a vendor key enabled supply it through `RuntimeConfigProvider` instead of stubbing `process.env`. Omitting the wrapper is how a test expresses "not configured", which removed some `vi.resetModules()` re-import workarounds that existed only because the key was read at module scope.

## References

- `apps/portal/src/SEBT.Portal.Web/src/lib/runtime-config.ts` — the server-side reader.
- `apps/portal/src/SEBT.Portal.Web/src/providers/RuntimeConfigProvider.tsx` — context and hook.
- `apps/portal/src/SEBT.Portal.Web/src/proxy.ts` — CSP built from the same variables.
- `scripts/ci/templates/web.config` — the IIS runtime environment block.
