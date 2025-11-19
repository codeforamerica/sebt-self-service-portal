# SEBT Portal Web

Frontend for the Summer EBT Self-Service Portal using USWDS with Figma design tokens.

## Quick Start

```bash
# Install dependencies
pnpm install

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

```
Figma → Tokens Studio Plugin → design/states/dc.json
                                      ↓
                        scripts/tokens-to-scss.js
                                      ↓
                         sass/_uswds-theme-dc.scss
                                      ↓
                      Vite build → Compiled CSS
```

### Update Tokens

```bash
# Regenerate SCSS from dc.json
pnpm tokens

# Smart caching: only regenerates if dc.json changed
```

## Project Structure

```
src/SEBT.Portal.Web/
├── design/states/        # Figma design tokens (JSON)
├── sass/                 # USWDS theme configuration
├── scripts/              # Token transformation script
├── src/
│   ├── main.ts          # App entry point
│   └── styles.scss      # Sass entry point
├── public/              # Static assets (auto-copied)
├── index.html           # HTML entry point
└── vite.config.ts       # Build configuration
```

## Key Features

- ✅ **Smart token caching**: Instant builds when tokens unchanged
- ✅ **Lightning CSS**: 5-10x faster CSS processing
- ✅ **sass-embedded**: 10-30% faster Sass compilation
- ✅ **HMR**: Instant hot module replacement
- ✅ **USWDS compliant**: Follows official custom compiler pattern

## Documentation

- [Token Management ADR](../../docs/adr/0003-design-token-management-with-figma-and-vite.md)
- [USWDS Custom Compiler Guide](https://designsystem.digital.gov/documentation/getting-started/developers/phase-two-compile/)
- [Figma Tokens Studio Docs](https://docs.tokens.studio/)

## Build Performance

- Build time: ~3s (with optimizations)
- CSS output: ~532 KB (minified + prefixed)
- Token transformation: <100ms (cached), ~200ms (regeneration)
- Browser support: IE11, Chrome 90+, Firefox 88+, Safari 14+, Edge 90+
