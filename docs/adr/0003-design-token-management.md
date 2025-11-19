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
- **USWDS compliance**: Must integrate with USWDS theming system and IE11 browser support
- **Build performance**: Fast iteration cycles for development and efficient CI/CD builds
- **Maintainability**: Clear, documented workflow that scales across 50+ state implementations

Without a robust token management system, each state implementation would require manual
translation of design decisions into code, creating maintenance burden and design-code drift.

## Decision

We will use **Figma Tokens Studio** for token management with a **Vite-based custom USWDS compiler**:

1. **Token Source of Truth**: Figma Tokens Studio plugin with GitHub sync
   - Designers manage all design tokens (colors, typography, spacing) in Figma
   - Tokens exported to `design/states/{state}.json` via GitHub integration
   - JSON format follows W3C Design Tokens Community Group specification

2. **Token Organization**: State-specific token files
   - One JSON file per state: `design/states/dc.json`, `design/states/ca.json`, etc.
   - Separation of `system` tokens (USWDS built-ins) from `theme` tokens (state customizations)
   - Hierarchical token structure supporting token references and composition

3. **Token Transformation**: Node.js script converting tokens to USWDS SCSS variables
   - Script: `src/SEBT.Portal.Web/scripts/tokens-to-scss.js`
   - Extracts `theme` object from state JSON (ignores `system` - USWDS provides these)
   - Converts to USWDS-compatible SCSS variable format (e.g., `$theme-color-primary-vivid`)
   - Smart caching: timestamp-based regeneration (only rebuilds if `dc.json` modified)

4. **USWDS Compilation**: Vite custom compiler following official USWDS patterns
   - Vite configuration: `src/SEBT.Portal.Web/vite.config.ts`
   - Sass loadPaths for USWDS package resolution
   - Token transformation runs as Vite plugin hook (`buildStart`)
   - Static asset copying (fonts, images, JavaScript) via `vite-plugin-static-copy`

5. **Build Optimizations**:
   - **Lightning CSS**: 5-10x faster CSS processing than PostCSS (Rust-based transformer)
   - **sass-embedded**: 10-30% faster Sass compilation (native Dart binary vs pure JavaScript)
   - **Smart caching**: Skips token transformation when source JSON unchanged
   - **Browser targets**: IE11, Chrome 90+, Firefox 88+, Safari 14+, Edge 90+ (USWDS requirements)

**Workflow**:
```
Figma Design → Tokens Studio Plugin → GitHub Sync → design/states/dc.json
                                                          ↓
                                   tokens-to-scss.js (Node.js transformation)
                                                          ↓
                                      sass/_uswds-theme-dc.scss (USWDS variables)
                                                          ↓
                                          Vite + Lightning CSS + sass-embedded
                                                          ↓
                                              Compiled CSS with DC theme
```

## Alternatives Considered

### Alternative 1: @uswds/compile (Official Gulp-based tooling)

The official USWDS compiler using Gulp for Sass compilation.

**Pros**:
- Official USWDS support and documentation
- Well-tested with USWDS ecosystem
- Established patterns and community examples

**Cons**:
- Gulp dependency (legacy build tool, declining adoption)
- Slower build times (no Lightning CSS or modern optimizations)
- Additional build step complexity (separate Gulp tasks + Vite)
- Requires `concurrently` to coordinate Gulp and Vite processes

**Why rejected**: USWDS documentation explicitly supports custom compilers as a valid approach.
Using Vite unifies the build toolchain, reduces dependencies, and provides significant
performance improvements. The custom compiler follows official USWDS patterns and maintains
full compatibility.

### Alternative 2: Style Dictionary

Platform-agnostic token transformation tool (used by design systems like Salesforce Lightning).

**Pros**:
- Industry standard tool with large community
- Platform-agnostic output (iOS, Android, Web, etc.)
- Extensive transformation and formatting options
- Well-documented and actively maintained

**Cons**:
- Additional tooling layer (Figma → Style Dictionary → USWDS)
- Manual Figma export workflow (no direct GitHub sync)
- Requires custom transforms for USWDS-specific patterns
- More complex setup for single-platform (web-only) use case

**Why rejected**: Figma Tokens Studio provides direct GitHub integration, eliminating manual
export steps. For our web-only use case, the additional platform-agnostic capabilities add
complexity without benefit. The custom Node.js transformation script is simpler and more
maintainable for this specific workflow.

### Alternative 3: CSS Custom Properties at Runtime

Use CSS custom properties (CSS variables) for theming instead of build-time Sass compilation.

**Pros**:
- Dynamic theming without rebuild (switch themes at runtime)
- Simpler build process (no token transformation needed)
- Modern web standard with good browser support

**Cons**:
- **IE11 incompatibility**: USWDS requires IE11 support, which lacks CSS custom properties
- **USWDS architecture mismatch**: USWDS is designed for Sass compilation, not CSS variables
- Performance overhead (runtime variable resolution vs compiled CSS)
- Requires significant USWDS customization to work with CSS variables

**Why rejected**: USWDS browser support requirements include IE11, which does not support
CSS custom properties. Adapting USWDS to use CSS variables would require extensive
customization and maintenance burden. Build-time compilation is more performant and aligns
with USWDS architecture.

### Alternative 4: Manual SCSS Variables

Manually maintain SCSS variable files for each state theme.

**Pros**:
- Simple, no tooling required
- Full control over SCSS structure
- No dependency on external tools

**Cons**:
- **Manual design-to-code sync**: Designers update Figma, developers manually translate
- **High maintenance burden**: 50+ states × multiple token updates = significant overhead
- **Design-developer workflow friction**: No automated pipeline, prone to errors
- **Version drift**: Design and code implementations diverge over time

**Why rejected**: Does not scale to 50+ state implementations. Manual synchronization is
error-prone and creates bottleneck in design-development workflow. Automated pipeline is
essential for maintaining consistency and velocity.

## Consequences

### Positive

- **Single source of truth**: Figma becomes the authoritative source for all design tokens
- **Automated workflow**: Design changes sync to code automatically via GitHub
- **Fast builds**: Lightning CSS (5-10x faster) + sass-embedded (10-30% faster) + caching
- **USWDS compatibility**: Full compliance with USWDS theming and browser requirements
- **Scalable to 50+ states**: Same pattern replicable across all state implementations
- **Developer experience**: Modern tooling (Vite) with HMR for rapid iteration
- **Reduced maintenance**: Automated sync eliminates manual token updates

### Negative

- **Custom tooling maintenance**: Token transformation script requires ongoing maintenance
- **Figma Tokens Studio dependency**: Reliance on third-party Figma plugin
- **Learning curve**: Team must understand token transformation and Vite configuration
- **Format coupling**: Tightly coupled to Figma Tokens Studio JSON format

### Risks and Mitigation

**Risk**: Figma Tokens Studio plugin abandonment or breaking changes
**Mitigation**: JSON token format is portable. Migration path to Style Dictionary exists if
needed. Token transformation script is independent of plugin (only reads JSON).

**Risk**: USWDS major version changes breaking custom compiler compatibility
**Mitigation**: Custom compiler follows official USWDS custom compiler patterns from USWDS
documentation. Pattern is stable across USWDS versions. Vite configuration uses USWDS-documented
loadPaths approach.

**Risk**: State-specific token conflicts or naming collisions
**Mitigation**: Establish clear token naming conventions documented in design system. Token
transformation script validates naming patterns. USWDS namespace (`theme-color-*`) prevents
collisions with USWDS core.

**Risk**: Token transformation script bugs affecting all state implementations
**Mitigation**: Comprehensive script documentation and examples. Version control tracks all
changes. Smart caching allows quick rollback (delete cached SCSS to regenerate).

## Implementation References

**Key Files**:
- Token transformation: `src/SEBT.Portal.Web/scripts/tokens-to-scss.js`
- Vite configuration: `src/SEBT.Portal.Web/vite.config.ts`
- USWDS theme loader: `src/SEBT.Portal.Web/sass/_uswds-theme.scss`
- Sass entry point: `src/SEBT.Portal.Web/src/styles.scss`
- DC state tokens: `src/SEBT.Portal.Web/design/states/dc.json`

**Performance Metrics** (DC theme compilation):
- Build time: ~3s (with optimizations)
- CSS output: ~532 KB (minified and prefixed)
- Token transformation: <100ms (cached), ~200ms (regeneration)
- Cache hit rate: >95% in development (tokens change infrequently)

**Dependencies**:
- `@uswds/uswds@^3.8.0` - USWDS design system
- `vite@^7.2.2` - Build tool and dev server
- `sass-embedded@^1.93.3` - Fast Sass compiler
- `lightningcss@^1.30.2` - Fast CSS transformer
- `vite-plugin-static-copy@^3.1.4` - Static asset copying

**Documentation**:
- USWDS Custom Compiler: https://designsystem.digital.gov/documentation/getting-started/developers/phase-two-compile/
- Figma Tokens Studio: https://docs.tokens.studio/
- Token transformation workflow: Comments in `scripts/tokens-to-scss.js`

## Related ADRs

- **ADR 0002**: Adopt Clean Architecture - Establishes project structure and separation of concerns
- **Future**: State Customization Strategy - Will reference this token implementation pattern

## Notes

This ADR documents the **initial implementation for DC (District of Columbia)** as the first
state. The pattern established here will be replicated for all subsequent state implementations
(CA, TX, FL, etc.). Each state will have its own `design/states/{state}.json` file following
the same structure and transformation workflow.

The decision to use Vite over @uswds/compile was made after reviewing official USWDS
documentation that explicitly supports custom compilers as a valid approach. This is not
a deviation from USWDS standards, but rather an implementation of the recommended custom
compiler pattern using modern tooling.
