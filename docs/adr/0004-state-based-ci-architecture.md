# 4. State-based CI architecture with branch-driven builds

Date: 2025-11-25

## Status

Accepted

## Context

The Summer EBT Self-Service Portal serves multiple states (DC, CO, and future states), each with different infrastructure requirements, deployment preferences, and operational constraints. States may prefer Docker-based builds or native tooling, require different versions of dependencies, or have state-specific feature flags and configurations.

Key requirements:
- **Multi-state deployment**: Each state needs independent build and deployment pipelines
- **Infrastructure flexibility**: Some states require Docker (containerized), others prefer native builds
- **Efficient CI usage**: Avoid building all states when changes affect only one state
- **Scalability**: Clear workflow that scales as new states are added (50+ potential states)
- **Unified codebase**: Single `main` branch contains all code (shared and state-specific)

Without a state-aware CI system, every code change would trigger builds for all states, wasting CI resources and slowing down state-specific development cycles.

## Decision

We will use **config-driven, branch-based CI** with automatic state discovery and conditional builds.

**Core Architecture**:
- **State Configurations**: YAML files per state (`config/states/{state}.yaml`) define infrastructure, versions, and build settings
- **Branch Pattern Detection**: Branch names determine which states to build (`deploy/dc-*` → DC only, `main` → all states)
- **Matrix Strategy**: GitHub Actions matrix automatically discovers states and builds in parallel
- **Conditional Execution**: Docker vs native builds based on state config (`use_docker: true|false`)
- **Reusable Scripts**: Bash scripts work across all environments (Docker, native, local via ACT)

**Branch Strategy**:
- `deploy/{state}-*` → Builds only specified state (e.g., `deploy/dc-feature` builds DC only)
- `feature/*`, `main` → Builds all states (validates shared code changes)
- `main` = Production source for all states (shared + state-specific code)

**Workflow**: `Branch → State Discovery → Load Config → Conditional Build (Docker or Native) → Artifacts`

## Alternatives Considered

### Alternative 1: Separate Repositories Per State
**Why rejected**: Creates code duplication and makes shared updates extremely difficult. Shared authentication, UI components, and business logic would diverge across 50+ repositories. Version management becomes a coordination nightmare.

### Alternative 2: Monolithic Workflow (One Size Fits All)
**Why rejected**: Forces all states into same infrastructure (either all Docker or all native). Some states have contractual requirements or existing infrastructure that mandate specific build approaches. Inflexible for state autonomy.

### Alternative 3: Reusable Workflows (GitHub Actions)
**Why rejected**: While reusable workflows are powerful, they require explicit invocation for each state. Our config-driven approach with automatic discovery is more maintainable at scale (50+ states) and reduces workflow duplication.

### Alternative 4: Manual Workflow Selection
**Why rejected**: Requires developers to manually specify states for each build. Branch-based automatic detection is less error-prone and provides faster feedback loops for state-specific development.

### Alternative 5: Path-Based Triggers
**Why rejected**: Would require reorganizing codebase into state-specific directories (`src/states/dc/`, `src/states/co/`), causing significant code duplication. Feature flags and config-driven runtime behavior is more maintainable.

## Consequences

### Positive
- **Efficient CI usage**: State-specific branches build only affected state (50% fewer builds)
- **Infrastructure flexibility**: States choose Docker or native based on their requirements
- **Independent versions**: Each state controls Node, pnpm, and .NET versions independently
- **Scalable to 50+ states**: Same pattern replicable; new states auto-discovered
- **Fast feedback loops**: Developers get targeted CI results in minutes, not tens of minutes
- **Unified codebase**: Single source of truth in `main` prevents code drift

### Negative
- **Branch naming discipline**: Team must follow `deploy/{state}-*` convention for targeted builds
- **Merge coordination**: Changes to shared code require validation across all states
- **Config management**: Each state config must be maintained and kept consistent with infrastructure

### Risks and Mitigation
**Risk**: Developers forget branch naming convention and accidentally build all states
**Mitigation**: Documentation, PR templates, and git hooks reminder. Cost is manageable (extra CI time, not broken builds)

**Risk**: State-specific code in `main` causes conflicts between states
**Mitigation**: Feature flags and config-driven behavior prevent runtime conflicts. Code reviews enforce separation of concerns.

**Risk**: Config drift between states makes debugging difficult
**Mitigation**: Template file (`_template.yaml`) and best practices documentation. Version field tracks state-specific deployments.

**Risk**: CI complexity increases maintenance burden
**Mitigation**: Reusable scripts and single workflow file. ACT for local testing before pushing.

## References

**Implementation Details**: See `docs/development/state-ci.md` for workflow details, configuration reference, and usage examples.

**Key Files**:
- `.github/workflows/state-ci.yaml` - Unified workflow with state discovery
- `config/states/*.yaml` - Per-state configuration files
- `scripts/ci/*.sh` - Reusable build and test scripts

## Related ADRs
- **ADR 0002**: Adopt Clean Architecture - Complements state isolation strategy

## Notes
This ADR documents the production-ready implementation for DC and CO as the first states. The pattern scales to all future state implementations. Local testing via ACT (`pnpm ci:test:state:dc`) allows developers to validate CI changes before pushing.
