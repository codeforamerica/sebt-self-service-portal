# 36. Let a state own its design tokens

Date: 2026-11-05

## Status

Proposed. Extension of [ADR 0003](0003-design-token-management.md). Result of the Spike SEBT-765.

## Context

[ADR 0003](0003-design-token-management.md) made Figma the source of truth for design tokens. Tokens Studio syncs each state's tokens from Figma to `packages/design-system/design/states/{state}.json`, and build scripts turn that file into CSS, Sass, and fonts.

That flow still works for states maintained by the team. It does not carry over to a state that maintains the portal itself after handoff. The Figma files are not part of the handoff, and Tokens Studio syncs into the portal repository, not the state's. A handed-off state needs its tokens in a format it can change with whatever tools it chooses.

The plan for a handed-off state is one asset repository per state, for example [codeforamerica/sebt-self-service-portal-co-assets](https://github.com/codeforamerica/sebt-self-service-portal-co-assets). Eventually this repository holds all of the state's own files: tokens, content, images, etc. The portal repository becomes the shared engine. The state's repository drives its own build: its CI checks out the portal and builds it with the state's files.

This ADR covers tokens only.

### The portal and the enrollment checker build differently

It's worth noting the two apps consume tokens in different ways, and this limits what we can do now.

- **Enrollment checker:** Each build is a static export for one state (`NEXT_PUBLIC_STATE`). Its `prebuild` generates tokens, Sass, and fonts for that state only.
- **Portal:** One build serves every state, and `STATE` is read at runtime (see [ADR 0023](0023-runtime-client-config.md)). Therefore its `prebuild` runs `generate-theme-css.js`, which compiles a stylesheet for **every** state in `STATE_CODES` into `public/themes/`. The server selects one at request time.

So a portal build for DC also compiles Colorado's theme. That build has no access to Colorado's asset repository.

## Decision

**A handed-off state keeps its token JSON file in its own repository. The build reads it through an environment variable.**

### The token file stays the contract

The state's repository contains `tokens/{state}.json`. It has the same format that Tokens Studio writes today. No new format or tool is needed, and the generator scripts do not change how they read the file.

### One loader decides where a state's tokens come from

`packages/design-system/design/scripts/state-token-source.js` exports `resolveStateTokens(state)`. The four scripts the two apps' builds run use it: `generate-tokens.js`, `generate-sass-tokens.js`, `generate-theme-css.js`, and `generate-fonts.js`. So do `generate-token-types.js` and `validate-tokens.js`, which exist but no build hook runs.

- If `EXTERNAL_TOKENS_DIR_{STATE}` is set (for example `EXTERNAL_TOKENS_DIR_CO`), the loader reads `{state}.json` from that directory. A relative path resolves against the portal repository root.
- If that variable is set but the file is missing, the build fails. It never falls back to the copy in the portal. That copy may be out of date for a state that maintains its tokens elsewhere.
- If the variable is not set, the loader reads `design/states/{state}.json`, as before.

Generated files record the source in their header, for example `Source: $EXTERNAL_TOKENS_DIR_CO/co.json`. `generate-tokens.js` also compares this header to decide whether `tokens.css` is stale, because modification times alone cannot detect a change of state or of source.

### Only the state being built is required

`generate-theme-css.js` requires tokens for `STATE`. It compiles every other state whose tokens are available and skips the rest. This lets a state's CI build the portal without the tokens of other states.

### The portal keeps its copy of a handed-off state's tokens, for now

`design/states/co.json` stays in the portal repository, because a portal build for any other state still compiles Colorado's theme (see [The portal and the enrollment checker build differently](#the-portal-and-the-enrollment-checker-build-differently)). We accept that this copy can fall behind Colorado's real tokens. Builds from Colorado's asset repository set `EXTERNAL_TOKENS_DIR_CO`. Colorado builds from the portal's own CI still use the copy until deployment moves to the asset repository.

Delete the copy when each portal build serves only one state. That change goes against the state-agnostic build in [ADR 0023](0023-runtime-client-config.md), so it needs its own decision.

### States the team designs for do not change

Figma and Tokens Studio stay the primary flow for new states, as in [ADR 0003](0003-design-token-management.md).

### Proof of concept

`codeforamerica/sebt-self-service-portal-co-assets` contains `tokens/co.json` and `.github/workflows/build.yaml`. The workflow checks out the portal, sets `EXTERNAL_TOKENS_DIR_CO`, and runs the full `pnpm build` for the portal and the enrollment checker. After each build it checks that the generated files name the external source. The external `co.json` is identical to the portal's copy, so the styling does not change.

## Alternatives Considered

### Alternative 1: An override file applied on top of the portal's state JSON

The state keeps a small file with only the values it changes, and the build merges it over `design/states/{state}.json`.

**Why rejected**: The state's real tokens would be in two places, and neither file alone shows what ships. When the base file changes, a state's overrides can conflict with it or silently stop applying, and nobody owns that merge. It would also need a merge format and rules that no existing tool understands. A complete file in the state's repository has one owner and needs no new tooling.

### Alternative 2: Keep the state's tokens in the portal repository and accept state edits by pull request

**Why rejected for now**: This keeps the state dependent on Code for America to review and release changes to its own branding, and goes against one repository per state. It is still possible if a state prefers it, because the default path is unchanged.

### Alternative 3: Build the portal separately for each state now

This would remove the need to keep a copy of Colorado's tokens in the portal.

**Why deferred**: It reverses the state-agnostic build in [ADR 0023](0023-runtime-client-config.md) and changes deployment for every state. It is larger than this ticket.

## Consequences

### Positive

- A handed-off state can change its tokens without a pull request to the portal.
- The token format, the generator scripts, and the Figma flow for other states do not change.
- If the external file is missing, the build fails clearly. It does not silently use stale tokens.
- The same pattern can extend to other state-owned files later.

### Negative

- Until each portal build serves only one state, the portal keeps a copy of each handed-off state's tokens, and that copy can drift.
- The state's repository has to keep its CI up to date with the portal's build, for example the Node and pnpm versions.
- Fonts are still in the portal repository. A state can choose only a font whose files the portal already has, or must ask us to add one.
- Nothing checks a state-owned token file beyond whether the build succeeds. `validate-tokens.js` exists, but no build hook runs it and it is out of date: it predates the semantic button tokens and reports their valid references (such as `theme-primary-dark` and `white`) as errors, so it fails for every state today.

### Open questions

- How do we expect partners to work with the portal after handoff?
  - This ADR assumes they keep only their own assets and build against our portal. If they fork the portal, they can edit `design/states/{state}.json` in their fork and do not need `EXTERNAL_TOKENS_DIR_{STATE}`. If they send changes to us by pull request, Alternative 2 fits better.
  - If they keep only their own assets, should the state's CI produce a build that is deployed, or only check that the portal builds? Today it only checks.

## Related ADRs

- [ADR 0003](0003-design-token-management.md): the Figma and Tokens Studio flow that this ADR extends.
- [ADR 0023](0023-runtime-client-config.md): why one portal build serves every state.
- [ADR 0022](0022-multi-state-plugin-approach.md): the plugin model for other state-specific behavior.
- [ADR 0032](0032-state-based-ci-architecture.md): how the portal's CI builds per state.
