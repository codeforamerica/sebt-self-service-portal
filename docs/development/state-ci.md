# State-Based CI System

CI configuration system that lets state deployments customize infrastructure preferences (Docker vs native builds) and build configuration through YAML files.

`build-and-test` (the job this doc mostly covers) runs **once** per PR/push, not once per state — DC and CO's build/test configuration was byte-identical, so matrixing it just doubled CI time for no extra coverage. See [ADR 0022](../adr/0022-dedupe-build-and-test-across-states.md) for why, and [ADR 0004](../adr/0004-state-based-ci-architecture.md) for the original per-state matrix architecture this narrows. Playwright E2E (`playwright-e2e.yaml`) and the enrollment-checker job are genuinely state-aware (they set `STATE`/`NEXT_PUBLIC_STATE`) and still run per state — this doc doesn't cover them.

## Quick Start

### Test State CI Locally

```bash
pnpm ci:test:states   # Runs the build-and-test job via ACT
pnpm ci:validate      # Dry-run all workflows
```

---

## State Configuration Reference

### File Location

```
.github/config/states/
├── _template.yaml     # Schema reference for a state config
└── state-config.yaml  # Shared build/test reference used by build-and-test
```

`state-config.yaml` is the one file `build-and-test` reads. It isn't state-specific today (see ADR 0022) — if a state's build ever needs to genuinely diverge (different Docker/native choice, different tool versions, different build flags), reinstate per-state config files and a matrix in `build-and-test` rather than adding conditionals to this one file.

### Configuration Schema

```yaml
# State identifier (informational only)
state: STATE_CODE
version: "1.0.0"  # State-specific version tracking

# Infrastructure configuration
infrastructure:
  use_docker: true|false          # Docker or native builds

  docker:                          # Docker-specific (if use_docker: true)
    registry: "ghcr.io/..."
    network_mode: "bridge|host"
    build_args: {}

  native:                          # Native-specific (if use_docker: false)
    install_path: "/opt/sebt"
    cache_dir: "/var/cache/sebt"

# Version requirements
versions:
  node: "24.x"                     # Node.js version
  pnpm: "10.x"                     # pnpm version
  dotnet: "10.0.x"                 # .NET SDK version

# Build configuration
build:
  configuration: "Release|Debug"   # Build configuration
  frontend_flags: ""               # Additional frontend flags
  backend_flags: ""                # Additional backend flags
  skip_tests: false                # Skip tests (not recommended)

# Environment variables (not currently read by any CI job)
environment:
  API_BASE_URL: "https://..."      # API endpoint
  features:                        # Feature flags
    multi_language: false
    advanced_search: false
    experimental_ui: false

# Deployment (not currently read by any CI job)
deployment:
  enabled: false
  target: "production|staging"
  notify_on_success: true
  notify_on_failure: true
```

`build-and-test` only reads `infrastructure.use_docker`, `versions.*`, and `build.*`. `environment.*` and `deployment.*` are documented here for schema completeness but aren't consumed by any workflow — don't assume changing them has an effect.

---

## How It Works

### Configuration Loading

```yaml
# .github/workflows/state-ci.yaml
- name: Load state configuration
  run: |
    yq eval '.infrastructure.use_docker' .github/config/states/state-config.yaml
    yq eval '.versions.node' .github/config/states/state-config.yaml
    # ... load all config values
```

### Conditional Build

Based on `use_docker`, choose build path:

```yaml
# Docker path
- name: Build (Docker)
  if: steps.config.outputs.use_docker == 'true'
  run: |
    docker run --rm \
      -v ${{ github.workspace }}:/workspace \
      node:${{ steps.config.outputs.node_version }}-alpine \
      ./.github/workflows/scripts/build-frontend.sh

# Native path
- name: Build (Native)
  if: steps.config.outputs.use_docker != 'true'
  uses: actions/setup-node@v4
  with:
    node-version: ${{ steps.config.outputs.node_version }}
  run: ./.github/workflows/scripts/build-frontend.sh
```

Both DC and CO currently set `use_docker: false`, so the native path is what actually runs.

---

## Testing

### Local Testing with ACT

```bash
pnpm ci:test:states   # Runs the build-and-test job
pnpm ci:validate      # Dry run (show what would execute)
```

### GitHub Actions

Runs on every push to `main`/`deploy/*` and every PR to `main`. Path filtering (`changes` job) skips backend/frontend/enrollment-specific steps when their trees are untouched — see the top of `state-ci.yaml`.

---

## Troubleshooting

### Build Fails

**Debug**:
1. Check `.github/config/states/state-config.yaml` for typos.
2. Test locally: `pnpm ci:test:states`.
3. Check workflow logs for the config values `yq` loaded.
4. Verify versions are available (e.g., Node 24.x exists).

### Docker Registry Issues

**Symptom**: Docker pull fails (only relevant if `use_docker: true`).

**Solution**: Update the registry in `state-config.yaml`:
```yaml
infrastructure:
  docker:
    registry: "YOUR_REGISTRY"
```

### Version Not Available

**Symptom**: `Node version 25.x not found`

**Solution**: Update `state-config.yaml`:
```yaml
versions:
  node: "24.x"  # Use current LTS
```
