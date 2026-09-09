# Design Token Scripts

## Overview

These scripts manage the Figma design token pipeline for multi-state deployments.

## Architecture: Build-Time State Detection

**Key Principle:** Each state gets its own build with state-specific design tokens baked in at build time.

```
Separate Builds per State
┌─────────────────┐          ┌─────────────────┐
│ STATE=dc        │          │ STATE=co        │
│ pnpm build      │          │ pnpm build      │
│ ↓               │          │ ↓               │
│ dc.sebt.gov     │          │ co.sebt.gov     │
└─────────────────┘          └─────────────────┘
```

## Scripts

### `generate-tokens.js` (Development)

Generates tokens for a single state during development.

```bash
# Usage
STATE=dc pnpm tokens      # Generate DC tokens
STATE=co pnpm tokens      # Generate CO tokens

# Auto-runs before dev server
pnpm dev                  # Uses STATE from .env
```

**When it runs:**

- `predev` hook (before `pnpm dev`)
- Manual: `pnpm tokens`

### `generate-all-tokens.js` (CI/CD)

Generates tokens for ALL states in CI/CD pipelines for validation.

```bash
# Usage
pnpm tokens:all           # Generate all state tokens for CI validation

# Not used in normal builds (each build only generates its own state)
```

**When it runs:**

- CI/CD validation workflows
- Manual: `pnpm tokens:all` for testing all state configs

## Token Generation Flow

```
1. Figma Design
   ↓ Figma Tokens Studio Plugin
2. design/states/{state}.json (Git)
   ↓ generate-tokens.js (STATE={state})
3. design/tokens.css (CSS custom properties)
   design/sass/_uswds-theme-{state}.scss (SASS variables)
   design/sass/_uswds-theme-semantic.scss (semantic component tokens)
   ↓ Next.js Build
4. Compiled CSS with state-specific tokens
   ↓ Deployment
5. State-specific build deployed to subdomain
```

## Semantic Component Tokens

Most `theme-*` tokens map to USWDS settings and flow into `_uswds-settings.scss`. Semantic component tokens are the exception: USWDS has no settings for them, so `generate-sass-tokens.js` routes them into `design/sass/_uswds-theme-semantic.scss` as plain SASS variables for component SCSS to consume.

```scss
@use '../uswds-theme-semantic' as semantic;

.usa-button {
  background-color: color(semantic.$theme-button-bg);
}
```

- **The set** is the button color slots (`SEMANTIC_COMPONENT_TOKENS` in `generate-sass-tokens.js`): solid `bg`/`bg-hover`/`bg-active`/`text`, outline `bg`/`border`/`border-hover`/`border-active`/`text`/`bg-hover`/`bg-active`.
- **Every state defines all of them.** A missing token fails the generator by name instead of surfacing as an undefined-variable error deep in the Sass compile.
- **Values are token references, not raw hex:** a theme role like `{theme-secondary-dark}` or a system token like `{gold-20v}`. Prefer theme roles so buttons keep following the state's palette ladders.
- **Hover/active text and disabled colors are deliberately excluded.** USWDS base rules own hover/active text; disabled colors sit on the USWDS `disabled-*` roles, which mean the same thing in every state.
- **Contrast (WCAG 2.1 AA):** button label text must clear 4.5:1 on its background in every pairing, including USWDS's hover/active text over the token backgrounds. DC and CO are checked as shipped; check any new state before deploying.

## Deployment

### State-Specific Build Process

```bash
# CI/CD: Build per state
STATE=dc pnpm build       # Build for DC with DC tokens
STATE=co pnpm build       # Build for CO with CO tokens

# Each build is deployed to its own subdomain
# dc.sebt.gov  → DC build
# co.sebt.gov  → CO build
```

## Adding New States

**1. Add Token File**

```bash
# Add Figma tokens to Git
design/states/va.json
```

**2. Update Configuration**

```javascript
// scripts/generate-all-tokens.js
const STATES = ['dc', 'co', 'va'] // Add 'va'
```

The new state's `theme` object must include the complete semantic component token set (see "Semantic Component Tokens" above); the generator lists any missing ones by name. Button colors need no SCSS changes.

**3. Build and Deploy**

```bash
STATE=va pnpm build     # Build for VA
# Deploy VA build to va.sebt.gov
```

**That's it!** The token generation system automatically handles the new state.

## Token File Structure

Expected structure from Figma Tokens Studio:

```json
{
  "global": {
    "color": {
      "primary": { "value": "#1a4480" },
      "secondary": { "value": "#c9c9c9" }
    },
    "font": {
      "family": {
        "sans": { "value": "Public Sans, sans-serif" }
      }
    }
  }
}
```

## Environment Variables

- `STATE` or `NEXT_PUBLIC_STATE`: State code (dc, co, va, etc.)
- Used at **build time** to generate state-specific tokens
- Each state gets its own separate build with baked-in tokens

## Reference

- [ADR 0003: Design Token Management](../../docs/adr/0003-design-token-management.md)
- [Figma Tokens Studio](https://docs.tokens.studio/)
- [USWDS Theming](https://designsystem.digital.gov/documentation/settings/)
