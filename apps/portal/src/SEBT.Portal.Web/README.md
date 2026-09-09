# SEBT Portal Web

Frontend for the Summer EBT Self-Service Portal using Next.js 16 with USWDS and Figma design tokens.

## Quick Start

```bash
# Install dependencies
pnpm install

# Configure your local state
cp .env.example .env
# Edit .env to set STATE=dc or STATE=co

# Start dev server (http://localhost:3000)
pnpm dev

# Build for production
pnpm build

# Preview production build
pnpm start
```

## Tech Stack

- **Framework**: Next.js 16 with App Router + React 19
- **Optimization**: React Compiler 1.0 (automatic memoization)
- **Design System**: USWDS 3.8
- **Styling**: SASS with USWDS theme system
- **Design Tokens**: Figma Tokens Studio → GitHub sync
- **Testing**: Vitest (unit) + Playwright (E2E)
- **Type Safety**: TypeScript strict mode + T3 env validation

## Design Token Workflow

Each state deployment uses a single token file - tokens are generated automatically during build.

```
Figma → Tokens Studio Plugin → design/states/{state}.json (committed to git)
                                      ↓
                        pnpm build (auto-generates during build)
                                      ↓
                         design/tokens.css (gitignored)
                         design/sass/_uswds-theme-{state}.scss (gitignored)
                                      ↓
                      Next.js build → Compiled CSS
```

### Building for Production

```bash
# Build for specific state (auto-generates tokens)
pnpm build            # Uses STATE from .env

# Or use environment variable
STATE=co pnpm build

# Default build uses DC if STATE not set
```

### Local Development

**State Configuration:** Your local state is configured via `.env` file. Next.js automatically loads this file.

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
pnpm tokens

# Generate tokens for all states (CI only)
pnpm tokens:all

# Or use the script directly
node design/scripts/generate-tokens.js

# Smart caching: only regenerates if state JSON changed
```

## Project Structure

```
src/SEBT.Portal.Web/
├── design/
│   ├── states/              # Figma design tokens (JSON)
│   │   ├── dc.json         # District of Columbia
│   │   └── co.json         # Colorado
│   ├── tokens.css          # Auto-generated CSS custom properties (gitignored)
│   ├── scripts/            # Token generation and asset scripts
│   │   ├── generate-tokens.js
│   │   ├── generate-all-tokens.js
│   │   ├── build-all-states.sh
│   │   └── copy-uswds-assets.sh
│   └── sass/               # USWDS theme configuration
│       ├── _uswds-theme-dc.scss   # Auto-generated DC theme (gitignored)
│       └── _uswds-theme.scss      # Theme loader
├── src/
│   ├── app/                # Next.js App Router pages + layouts
│   │   ├── page.tsx       # Home page
│   │   ├── page.test.tsx  # Co-located unit test
│   │   ├── layout.tsx     # Root layout
│   │   ├── fonts.ts       # Next.js font optimization
│   │   └── globals.css    # Global styles
│   ├── mocks/              # MSW handlers for testing
│   │   ├── handlers.ts
│   │   ├── handlers.test.ts
│   │   └── server.ts
│   ├── lib/                # Shared utilities
│   │   └── axe.ts         # Runtime accessibility monitoring
│   ├── test-setup.ts       # Vitest global setup
│   └── env.ts              # Type-safe environment validation
├── e2e/                    # Playwright E2E tests
├── public/                 # Static assets (USWDS fonts, images)
├── .github/
│   ├── workflows/scripts/  # CI build scripts
│   └── config/states/      # State configurations
└── next.config.ts          # Next.js configuration
```

## Testing

### Unit Tests (Vitest)

```bash
pnpm test             # Run tests in watch mode
pnpm test:ui          # Open Vitest UI
pnpm test:coverage    # Generate coverage report
```

**Co-located Pattern**: Tests live next to components (e.g., `page.test.tsx` next to `page.tsx`)

### E2E Tests (Playwright)

```bash
pnpm test:e2e         # Run E2E tests
pnpm test:e2e:ui      # Open Playwright UI
```

**Cross-browser**: Tests run on Chrome, Firefox, Safari, Edge + mobile viewports

### Accessibility Testing

```bash
pnpm test:a11y        # Run pa11y accessibility tests
```

**Runtime Monitoring**: axe-core enabled in development (see `src/lib/axe.ts`)

## Key Features

- ✅ **Multi-state support**: Generate themes for any state (DC, CO, etc.)
- ✅ **Smart token caching**: Instant builds when tokens unchanged
- ✅ **Server-side rendering**: SEO optimization and accessibility
- ✅ **React Compiler**: Automatic memoization (no manual `useMemo`/`useCallback`)
- ✅ **Type safety**: Runtime environment validation with T3 env + Zod
- ✅ **Pre-commit gates**: ESLint, Prettier, TypeScript, token regeneration
- ✅ **USWDS compliant**: Follows official custom compiler pattern
- ✅ **Comprehensive testing**: Unit + E2E + accessibility testing

## Multi-State Deployment

Each state is a **separate deployment** with its own build. The build process automatically generates the correct SCSS for that state.

**Deployment Architecture:**

```
dc.portal.sebt.gov  → STATE=dc pnpm build → Uses design/states/dc.json
co.portal.sebt.gov  → STATE=co pnpm build → Uses design/states/co.json
```

**CI/CD Integration:**

```yaml
# Example GitHub Actions workflow
- name: Build DC Portal
  env:
    STATE: dc
  run: pnpm build # Auto-generates tokens from dc.json

- name: Build CO Portal
  env:
    STATE: co
  run: pnpm build # Auto-generates tokens from co.json
```

**What's committed to Git:**

- ✅ `design/states/*.json` - Source of truth for design tokens
- ✅ `design/scripts/` - Token transformation scripts
- ❌ `design/tokens.css` - Auto-generated CSS custom properties, gitignored
- ❌ `design/sass/_uswds-theme-*.scss` - Auto-generated SASS, gitignored

## Code Quality

### Pre-commit Gates (via Husky + lint-staged)

Automatic checks on changed files:

1. **TypeScript/TSX**: ESLint auto-fix → Prettier format → Type check
2. **CSS/SCSS**: Prettier format
3. **JSON/Markdown**: Prettier format
4. **SCSS in design/sass/**: Regenerate design tokens

### Manual Quality Checks

```bash
pnpm lint             # ESLint check
pnpm knip             # Find unused dependencies
pnpm analyze          # Bundle size analysis
pnpm lighthouse       # Lighthouse performance audit
```

## Documentation

- [Next.js Migration ADR](../../docs/adr/0005-nextjs-framework-migration.md)
- [Token Management ADR](../../docs/adr/0003-design-token-management.md)
- [State-Based CI](../../docs/development/state-ci.md)
- [USWDS Custom Compiler Guide](https://designsystem.digital.gov/documentation/getting-started/developers/phase-two-compile/)
- [Figma Tokens Studio Docs](https://docs.tokens.studio/)

## Performance

**React Compiler**: Automatic optimization enabled

- Build time: +15-30% (acceptable trade-off)
- Runtime: Eliminates manual memoization

**Bundle Analysis**: `pnpm analyze` generates visualization

**Standalone Output**: Self-contained `.next/standalone/` for Docker/native deployment
