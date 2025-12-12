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
   ↓ Next.js Build
4. Compiled CSS with state-specific tokens
   ↓ Deployment
5. State-specific build deployed to subdomain
```

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
