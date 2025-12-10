# 5. Next.js web application with production-grade tooling and quality gates

Date: 2025-12-05

## Status

Accepted

## Context

The Summer EBT Self-Service Portal web application requires a production-ready foundation that supports server-side rendering, multi-state deployment, USWDS integration, and comprehensive quality assurance. Government web applications have stringent requirements for accessibility (WCAG 2.1 AA), security hardening, SEO optimization, and deployment flexibility across different state infrastructures.

Key requirements:
- **Server-side rendering (SSR)**: SEO optimization and accessibility compliance through server-rendered HTML
- **Multi-state support**: Build-time state configuration with design token injection
- **USWDS integration**: Seamless SASS compilation with Figma design tokens
- **Type safety**: Runtime environment validation and TypeScript strict mode
- **Quality assurance**: Automated testing (unit + E2E), pre-commit validation, security scanning
- **Modern React**: React 19 with automatic optimization capabilities
- **Deployment flexibility**: Standalone builds compatible with Docker and native environments

## Decision

We will implement the web application using **Next.js 16 with App Router** as the foundation, with comprehensive tooling for production quality assurance.

**Core Architecture**:
- **Framework**: Next.js 16 with App Router and React Server Components
- **React Optimization**: React 19 with React Compiler 1.0 for automatic memoization
- **Build System**: Turbopack for development, standalone output for deployment
- **SASS Integration**: Custom configuration with USWDS package paths and design token system
- **State Management**: Build-time state injection via `NEXT_PUBLIC_STATE` environment variable

**Quality Infrastructure**:
- **Type Safety**: @t3-oss/env-nextjs with Zod schemas for environment validation
- **Unit Testing**: Vitest with jsdom environment and React Testing Library
- **E2E Testing**: Playwright with cross-browser support (Chrome, Firefox, Safari, Edge)
- **Code Quality**: ESLint with security plugin, Prettier, TypeScript strict mode
- **Pre-commit Gates**: lint-staged with ESLint, Prettier, TypeScript, and token regeneration
- **Accessibility**: jsx-a11y ESLint plugin with WCAG 2.1 AA compliance rules
- **Security**: ESLint security plugin, no `x-powered-by` header, strict CSP-ready

**Developer Experience**:
- **Hot Module Replacement**: Sub-second updates during development
- **Path Aliases**: `@/` imports for clean module resolution
- **Bundle Analysis**: `@next/bundle-analyzer` for production optimization
- **Dead Code Detection**: Knip integration for unused dependency tracking

**Workflow**: `STATE env var → Token Injection → SASS Compilation → SSR → Standalone Build → Docker/Native Deploy`

## Implementation Details

### Environment Management
Type-safe environment variables with runtime validation prevent deployment errors:
- Server-only variables: `NODE_ENV`
- Client variables: `NEXT_PUBLIC_STATE` (validated as 'dc' | 'co')
- Build-time transformation: `STATE` → `NEXT_PUBLIC_STATE` in next.config.ts
- Zod schema validation catches misconfigurations at build time

### USWDS Integration
Custom SASS configuration maintains full USWDS compatibility:
- Include paths: `sass/`, `node_modules/@uswds/uswds/packages/`, `node_modules/`
- Global `sass:math` injection for USWDS calculations
- State-specific token files compiled at build time
- Design tokens from Figma synchronized via ADR 0003 workflow

### Testing Strategy
Comprehensive testing pyramid with separation of concerns:
- **Unit Tests**: `tests/**/*.test.{ts,tsx}` via Vitest with jsdom
- **E2E Tests**: `tests-e2e/**/*.spec.ts` via Playwright
- **Coverage**: v8 provider with HTML/JSON/text reports (excluded from git)
- **CI Integration**: Pre-commit hooks run unit tests before commit

### Pre-commit Quality Gates
Eight-step validation pipeline via lint-staged:
1. **ESLint**: Auto-fix TypeScript/TSX issues with security rules
2. **Prettier**: Format code, CSS, SCSS, JSON, Markdown
3. **TypeScript**: Type checking with strict mode (no `any` types allowed)
4. **Token Regeneration**: Auto-rebuild design tokens when SCSS changes
5. **Unit Tests**: Full test suite validation
6. **Accessibility**: jsx-a11y checks for WCAG compliance
7. **Security**: Vulnerability detection via ESLint security plugin
8. **Dead Code**: Knip validation (manual, not in pre-commit)

### React Compiler Integration
Automatic optimization without manual memoization:
- Enabled via `reactCompiler: true` in next.config.ts
- React Compiler 1.0 stable as of Next.js 16
- SWC optimization only processes relevant files (JSX/Hooks)
- Build time increase: ~15-30% (acceptable for automatic optimization)

### Multi-State Build Support
Seamless integration with ADR 0004 state-based CI:
- `STATE=dc pnpm build` → DC-specific build with mint-cool-60v primary color
- `STATE=co pnpm build` → CO-specific build with state tokens
- Standalone output mode: Self-contained `.next/standalone/` directory
- Compatible with Docker and native deployment strategies

## Consequences

### Positive
- **Production-ready foundation**: SSR, type safety, testing, security out-of-the-box
- **Quality assurance**: Pre-commit gates prevent broken code from reaching repository
- **Accessibility compliance**: WCAG 2.1 AA rules enforced via ESLint and runtime testing
- **Security hardening**: Multiple layers (ESLint plugin, headers, dependency scanning)
- **Developer productivity**: HMR, path aliases, automatic formatting and linting
- **Automatic optimization**: React Compiler eliminates manual memoization burden
- **Multi-state ready**: Integrates with existing design token and CI workflows
- **Comprehensive testing**: Unit + E2E coverage with cross-browser validation

### Negative
- **Pre-commit overhead**: 3-8 seconds per commit (mitigated by lint-staged file filtering)
- **React Compiler build time**: 15-30% slower than pure SWC (acceptable trade-off)
- **Learning curve**: Team must understand App Router, Server Components, SSR patterns
- **SASS configuration**: Manual USWDS path setup required (documented in next.config.ts)

### Risks and Mitigation
**Risk**: Pre-commit hooks slow down rapid development
**Mitigation**: lint-staged only processes changed files. Git hooks can be bypassed with `--no-verify` for emergency commits (discouraged).

**Risk**: React Compiler edge cases in production
**Mitigation**: React Compiler 1.0 is stable. Can disable via config flag if issues arise. Next.js team validates compatibility.

**Risk**: Environment variable misconfiguration in production
**Mitigation**: @t3-oss/env-nextjs validates at build time, failing builds with clear error messages before deployment.

**Risk**: Test suite becomes slow as application grows
**Mitigation**: Vitest runs in parallel. E2E tests isolated from unit tests. CI caches dependencies for faster runs.

## References

**Implementation Location**: `src/SEBT.Portal.Web/`

**Key Configuration Files**:
- `next.config.ts` - Next.js with React Compiler, SASS, and bundle analyzer
- `src/env.ts` - Type-safe environment validation with Zod schemas
- `vitest.config.ts` - Unit testing with jsdom and React Testing Library
- `playwright.config.ts` - E2E testing across Chrome, Firefox, Safari, Edge
- `.lintstagedrc.json` - Pre-commit quality gates and automation
- `eslint.config.mjs` - Flat config with security and accessibility plugins
- `tsconfig.json` - TypeScript strict mode with 9 strict checks enabled

**Documentation**:
- [Next.js 16 Documentation](https://nextjs.org/docs)
- [React Compiler Documentation](https://react.dev/learn/react-compiler)
- [USWDS Documentation](https://designsystem.digital.gov/)

## Related ADRs
- **ADR 0003**: Design Token Management - USWDS SASS integration with Figma tokens
- **ADR 0004**: State-based CI Architecture - Standalone builds for multi-state deployment

## Notes
This ADR documents the production-ready web application foundation with DC as the reference implementation. All quality gates, testing infrastructure, and USWDS integration are operational. The foundation supports future features including authentication, server-side data fetching, API integration, and progressive enhancement for the multi-state portal system.
