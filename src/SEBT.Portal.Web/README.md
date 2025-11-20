# SEBT Portal Web

Frontend for the Summer EBT Self-Service Portal using USWDS with Figma design tokens.

## Quick Start

```bash
# Install dependencies
pnpm install

# Configure your local state
cp .env.example .env
# Edit .env to set STATE=dc or STATE=co

# Start dev server (http://localhost:5173)
pnpm dev

# Build for production
pnpm build

# Preview production build
pnpm preview
```

## Tech Stack

- **Framework**: Vite 7 + TypeScript
- **Design System**: USWDS 3.8
- **CSS**: Sass with Lightning CSS
- **Design Tokens**: Figma Tokens Studio → GitHub sync

## Design Token Workflow

Each state deployment uses a single token file - tokens are generated automatically during build.

```
Figma → Tokens Studio Plugin → design/states/{state}.json (committed to git)
                                      ↓
                        pnpm build:dc (auto-generates during build)
                                      ↓
                         sass/_uswds-theme-dc.scss (gitignored)
                                      ↓
                      Vite build → Compiled CSS
```

### Building for Production

```bash
# Build for specific state (auto-generates tokens)
pnpm web:build:dc    # DC deployment
pnpm web:build:co    # Colorado deployment

# Or use environment variable
STATE=co pnpm web:build

# Default build uses DC
pnpm web:build
```

### Local Development

**State Configuration:**
Your local state is configured via `.env` file. Vite automatically loads this file.

```bash
# .env file
STATE=dc  # or co

# Start dev server - uses state from .env
pnpm dev

# Override state temporarily
STATE=co pnpm dev
```

**Manual Token Generation (Development Only):**

```bash
# Regenerate tokens when you update design/states/*.json
pnpm tokens:dc
pnpm tokens:co

# Or use the script directly
node scripts/tokens-to-scss.js dc

# Smart caching: only regenerates if state JSON changed
```

## Project Structure

```
src/SEBT.Portal.Web/
├── design/states/        # Figma design tokens (JSON)
│   ├── dc.json          # District of Columbia
│   └── co.json          # Colorado (when added)
├── sass/                 # USWDS theme configuration
│   ├── _uswds-theme-dc.scss   # Auto-generated DC theme
│   └── _uswds-theme.scss      # Theme loader
├── scripts/
│   └── tokens-to-scss.js      # Token transformation script
├── src/
│   ├── main.ts          # App entry point
│   └── styles.scss      # Sass entry point
├── public/              # Static assets (auto-copied)
├── index.html           # HTML entry point
└── vite.config.ts       # Build configuration
```

## Key Features

- ✅ **Multi-state support**: Generate SCSS for any state (DC, CO, etc.)
- ✅ **Smart token caching**: Instant builds when tokens unchanged
- ✅ **Lightning CSS**: 5-10x faster CSS processing
- ✅ **sass-embedded**: 10-30% faster Sass compilation
- ✅ **HMR**: Instant hot module replacement
- ✅ **USWDS compliant**: Follows official custom compiler pattern

## Multi-State Deployment

Each state is a **separate deployment** with its own build. The build process automatically generates the correct SCSS for that state.

**Deployment Architecture:**
```
dc.portal.sebt.gov  → pnpm web:build:dc → Uses design/states/dc.json
co.portal.sebt.gov  → pnpm web:build:co → Uses design/states/co.json
```

**CI/CD Integration:**
```yaml
# Example GitHub Actions workflow
- name: Build DC Portal
  run: pnpm web:build:dc  # Auto-generates tokens from dc.json

- name: Build CO Portal
  run: pnpm web:build:co  # Auto-generates tokens from co.json
```

**What's committed to Git:**
- ✅ `design/states/*.json` - Source of truth for design tokens
- ✅ `scripts/tokens-to-scss.js` - Token transformation script
- ❌ `sass/_uswds-theme-*.scss` - Auto-generated, gitignored

## Documentation

- [Token Management ADR](../../docs/adr/0003-design-token-management-with-figma-and-vite.md)
- [USWDS Custom Compiler Guide](https://designsystem.digital.gov/documentation/getting-started/developers/phase-two-compile/)
- [Figma Tokens Studio Docs](https://docs.tokens.studio/)

## Build Performance

- Build time: ~3s (with optimizations)
- CSS output: ~532 KB (minified + prefixed)
- Token transformation: <100ms (cached), ~200ms (regeneration)
- Browser support: IE11, Chrome 90+, Firefox 88+, Safari 14+, Edge 90+
