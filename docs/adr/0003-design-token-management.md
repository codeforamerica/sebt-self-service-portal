# 3. Design token management with Figma Tokens Studio and Vite

Date: 2025-11-19

## Status

Accepted

## Context

The Summer EBT Self-Service Portal needs to support customization for 50+ states while
maintaining a consistent design system foundation. Each state requires its own visual identity
(colors, typography, spacing) while adhering to USWDS (U.S. Web Design System) standards and
federal accessibility requirements.

Key requirements:
- **Multi-state branding**: Each state needs customizable theme tokens (DC, CA, TX, etc.)
- **Designer-developer workflow**: Designers work in Figma, developers need automated sync
- **USWDS compliance**: Must integrate with USWDS theming system
- **Scalability**: Clear workflow that scales across 50+ state implementations

Without automated token management, each state implementation would require manual
translation of design decisions into code, creating maintenance burden and design-code drift.

## Decision

We will use **Figma Tokens Studio** for token management with a **Vite-based custom USWDS compiler**.

**Core Architecture**:
- **Token Source**: Figma Tokens Studio plugin with GitHub sync to `design/states/{state}.json`
- **Token Organization**: One JSON file per state following W3C Design Tokens specification
- **Transformation**: Node.js scripts convert tokens to USWDS SCSS variables and inject fonts
- **Compilation**: Vite compiles USWDS with state-specific theme tokens using modern build tooling
- **Optimization**: Lightning CSS, sass-embedded, PurgeCSS for production bundle optimization

**Workflow**: `Figma → Tokens Studio → GitHub → JSON → Transformation Scripts → SCSS → Vite → CSS Bundle`

## Alternatives Considered

### Alternative 1: @uswds/compile (Official Gulp-based tooling)
**Why rejected**: Gulp is a legacy build tool with slower build times. USWDS documentation
explicitly supports custom compilers. Vite unifies our toolchain and provides modern
performance optimizations while maintaining full USWDS compatibility.

### Alternative 2: Style Dictionary
**Why rejected**: Requires manual Figma export workflow (no GitHub sync). For our web-only
use case, the platform-agnostic capabilities add complexity without benefit. Figma Tokens
Studio's direct GitHub integration eliminates manual export steps.

### Alternative 3: CSS Custom Properties at Runtime
**Why rejected**: USWDS requires IE11 support, which lacks CSS custom property support.
Build-time compilation is more performant and aligns with USWDS architecture.

### Alternative 4: Manual SCSS Variables
**Why rejected**: Does not scale to 50+ state implementations. Manual synchronization is
error-prone and creates a bottleneck in the design-development workflow. Automated pipeline
is essential for maintaining consistency and velocity.

## Consequences

### Positive
- **Single source of truth**: Figma becomes the authoritative source for all design tokens
- **Automated workflow**: Design changes sync to code automatically via GitHub
- **USWDS compatibility**: Full compliance with USWDS theming and browser requirements
- **Scalable to 50+ states**: Same pattern replicable across all state implementations
- **Modern developer experience**: Vite with HMR for rapid iteration
- **Production optimization**: Automated bundle optimization with Lightning CSS and PurgeCSS

### Negative
- **Custom tooling maintenance**: Token transformation scripts require ongoing maintenance
- **Third-party dependency**: Reliance on Figma Tokens Studio plugin
- **Learning curve**: Team must understand token transformation and Vite configuration

### Risks and Mitigation
**Risk**: Figma Tokens Studio plugin abandonment
**Mitigation**: JSON format is portable; migration path to Style Dictionary exists

**Risk**: USWDS version changes breaking compatibility
**Mitigation**: Custom compiler follows official USWDS patterns documented by USWDS team

**Risk**: State-specific token conflicts
**Mitigation**: Clear naming conventions and USWDS namespace prevent collisions

## References

**Implementation Details**: See `docs/design-token-system.md` for technical specifications,
performance metrics, and implementation guide.

**Key Documentation**:
- [USWDS Custom Compiler Guide](https://designsystem.digital.gov/documentation/getting-started/developers/phase-two-compile/)
- [Figma Tokens Studio Documentation](https://docs.tokens.studio/)

## Related ADRs
- **ADR 0002**: Adopt Clean Architecture

## Notes
This ADR documents the production-ready implementation for DC as the first state. The pattern
will be replicated for all subsequent state implementations (CA, TX, FL, etc.).
