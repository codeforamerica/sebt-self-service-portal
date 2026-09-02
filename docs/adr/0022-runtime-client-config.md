# 22. Serve browser-facing config at request time instead of inlining it

Date: 2026-09-01

## Status

Accepted for the portal's browser-facing configuration, including the state it serves.

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

- **The state moves to runtime too, which needed the theming to change.** `STATE` is not merely a value: it selected the compiled USWDS stylesheet, the `next/font` faces, and the locale bundle. Sass configures `uswds-core` once per compilation — `@use ... with (...)` is singleton — so two themes cannot coexist in one stylesheet. Each state is therefore compiled to its own file (`public/themes/theme-{state}.css`, ~630 KB each) and linked once `STATE` is known at request time, so a visitor downloads one theme rather than every state's. Fonts for every state are declared in one generated module with `preload` off, since preloading is a build-time hint and would otherwise fetch faces this process will never render. Locale bundles already carried every state.

- **Client components read the state from the DOM.** `getState()` resolves `<html data-state>` first, then `process.env.STATE`, then `NEXT_PUBLIC_STATE`. The attribute is the browser's only runtime source; the server stamps it per request so both sides agree and hydration stays stable. Keeping the function's signature meant none of its 27 call sites changed. The `NEXT_PUBLIC_STATE` fallback remains for the enrollment checker, which deploys one static export per state and has no server to read env from.

- **The portal image is state-agnostic.** The Dockerfile no longer takes `ARG STATE`, and neither ECR build passes it. The image is identical for every state and can be promoted between them; the ECS task definition supplies `STATE`. (Each environment still pushes to its own ECR repository — consolidating those is infrastructure work outside this repo.)

## Consequences

- One frontend artifact runs in any environment **and as any state**. Verified: an artifact built with no client config and no `STATE` served `G-RUNTIME-AAA`/`smarty-aaa` then `G-RUNTIME-BBB`/`smarty-bbb`/`amp-bbb` (CSP widening for Amplitude only on the second run), and the same artifact served `data-state="dc"` with `theme-dc.css` and the DC title, then `data-state="co"` with `theme-co.css` and the CO title, purely from `STATE`.
- The USWDS theme is no longer part of the JS bundle's CSS; it is a static stylesheet linked at request time. Two consequences were closed rather than accepted: the link carries `?v=<build sha>` because the file has no content hash to bust caches on deploy, and the fonts moved out of `next/font` into `@font-face` rules inside each per-state stylesheet, with the layout preloading exactly that state's files from a generated manifest. Declaring every state's faces through `next/font` would have meant either preloading typefaces the process never renders, or no preloading at all. Nothing is lost by the move: the files were already self-hosted at stable paths under `public/fonts`, and `adjustFontFallback` was already disabled. `optimizeCss` was never enabled, so no critical-CSS inlining existed to lose.
- `STATE` was a required build input, so a missing value used to break the build. It is now supplied per deployment, and `instrumentation.ts` refuses to start in production without it — a misconfigured environment fails loudly at deploy instead of quietly serving DC branding.
- **Deployment coordination is required before this reaches production.** The values must now be set on the running container (Tofu / ECS task definition) for the Docker path, and in `web.config`'s `<environmentVariables>` on the host for the IIS path. Removing the build args without that leaves analytics, Socure, and Smarty switched off in deployed environments — the same silent failure as before, in the opposite direction.
- The `web.config` runtime env block is now load-bearing rather than inert. CLAUDE.md previously (and correctly) warned that it had no effect on browser code; that guidance is superseded for these variables.
- Tests that need a vendor key enabled supply it through `RuntimeConfigProvider` instead of stubbing `process.env`. Omitting the wrapper is how a test expresses "not configured", which removed some `vi.resetModules()` re-import workarounds that existed only because the key was read at module scope.

## References

- `apps/portal/src/SEBT.Portal.Web/src/lib/runtime-config.ts` — the server-side reader.
- `apps/portal/src/SEBT.Portal.Web/src/providers/RuntimeConfigProvider.tsx` — context and hook.
- `apps/portal/src/SEBT.Portal.Web/src/proxy.ts` — CSP built from the same variables.
- `scripts/ci/templates/web.config` — the IIS runtime environment block.
