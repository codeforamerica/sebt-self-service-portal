# Design Token Scripts

## Overview

These scripts manage the Figma design token pipeline for multi-state deployments.

## Architecture: Single Build, Runtime Configuration

**Key Principle:** Build once with all state tokens, configure state at deployment time via environment variable.

```
Single Build                  Runtime Configuration
┌──────────────┐             ┌─────────┐  ┌─────────┐
│              │             │ DC      │  │ CO      │
│  Next.js     │────────────▶│ Deploy  │  │ Deploy  │
│  (all tokens)│             │ STATE=dc│  │ STATE=co│
│              │             └─────────┘  └─────────┘
└──────────────┘
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

### `generate-all-tokens.js` (Production)

Generates tokens for ALL states for production builds.

```bash
# Usage
pnpm tokens:all           # Generate all state tokens

# Auto-runs before build
pnpm build                # Generates all tokens via prebuild
```

**When it runs:**

- `prebuild` hook (before `pnpm build`)
- Manual: `pnpm tokens:all`

## Token Generation Flow

```
1. Figma Design
   ↓ Figma Tokens Studio Plugin
2. design/states/{state}.json (Git)
   ↓ generate-all-tokens.js
3. sass/_uswds-theme-{state}.scss (All states)
   ↓ Next.js SASS Compilation
4. CSS with CSS Variables
   ↓ Runtime: data-state attribute
5. Correct tokens applied
```

## CSS Variables Approach

**Implementation Pattern:**

```scss
// sass/_uswds-theme.scss
:root {
  /* DC tokens as defaults */
  --color-primary: #1a4480;
  --font-family-sans: 'Public Sans', sans-serif;
}

[data-state='co'] {
  /* CO overrides */
  --color-primary: #0071bc;
  --font-family-sans: 'Roboto', sans-serif;
}

[data-state='va'] {
  /* VA overrides */
  --color-primary: #2e8540;
}
```

```tsx
// layout.tsx
<html data-state={process.env.NEXT_PUBLIC_STATE || 'dc'}>{children}</html>
```

## Deployment

### Single Build Process

```bash
# CI/CD: Build once
pnpm build                # Includes ALL state tokens

# Deploy multiple times with different config
STATE=dc pnpm start       # DC deployment
STATE=co pnpm start       # CO deployment
STATE=va pnpm start       # VA deployment (when added)
```

### Build Time Savings

- **Before:** 3 states = 3 builds = ~9 minutes
- **After:** 3 states = 1 build = ~3 minutes

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

**3. Deploy**

```bash
pnpm build              # All tokens auto-included
STATE=va pnpm start     # Deploy with VA config
```

**That's it!** No new build scripts or separate build artifacts needed.

## TODO: Implement Token Generation

The current scripts are placeholders. Implement the actual token transformation logic:

**Steps:**

1. Read `design/states/{state}.json`
2. Transform JSON tokens to SCSS variables
3. Write to `sass/_uswds-theme-{state}.scss` (gitignored)
4. Generate CSS variables in `:root` and `[data-state="{state}"]` blocks
5. Optional: Generate TypeScript types for design tokens

**Example Implementation:**

```javascript
// In generate-tokens.js or generate-all-tokens.js
import { readFileSync, writeFileSync } from 'fs'

function generateTokensForState(state) {
  // 1. Read token file
  const tokens = JSON.parse(readFileSync(`design/states/${state}.json`, 'utf8'))

  // 2. Transform to SCSS
  const scss = transformToScss(tokens, state)

  // 3. Write output
  writeFileSync(`sass/_uswds-theme-${state}.scss`, scss)
}
```

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
- Used at **runtime** to apply correct tokens via `data-state` attribute
- No longer used at **build time** (all tokens included in build)

## Reference

- [ADR 0003: Design Token Management](../../docs/adr/0003-design-token-management.md)
- [Figma Tokens Studio](https://docs.tokens.studio/)
- [USWDS Theming](https://designsystem.digital.gov/documentation/settings/)
