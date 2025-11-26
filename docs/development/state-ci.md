# State-Based CI System

Multi-state CI configuration system that allows each state to define their infrastructure preferences (Docker vs native builds) and build configuration through YAML files.

## Quick Start

### Branch Strategy for State-Specific Builds

The CI system uses **branch naming conventions** to determine which states to build:

```bash
# Single-state builds (only builds specified state)
deploy/dc           # Only DC
deploy/dc-feature   # Only DC
deploy/co-hotfix    # Only CO

# Multi-state builds (builds all states)
main                # All states
feature/*           # All states
fix/*               # All states
```

### Test State CI Locally

```bash
# Test all states
pnpm ci:test:states

# Test specific state
pnpm ci:test:state:dc
pnpm ci:test:state:co
```

### Add a New State

```bash
# 1. Copy template
cp config/states/_template.yaml config/states/va.yaml

# 2. Edit configuration
vim config/states/va.yaml

# 3. Commit and push
git add config/states/va.yaml
git commit -m "feat: add Virginia (VA) state configuration"
git push

# CI automatically discovers and builds for VA!
```

---

## Architecture

### Config-Driven System with Branch-Based Triggering

**Key Principle**: One workflow, multiple state configs, branch-based state selection

```
Branch → State Discovery → Workflow → Build (Docker or Native)
```

**Main Branch Strategy:**
- `main` = Production source for **all states**
- Contains: Shared code + DC features + CO features + state configs
- Each state deployment uses only what it needs via config/feature flags

**Branch Patterns:**
- `deploy/dc-*` → Only DC builds (fast feedback)
- `deploy/co-*` → Only CO builds (fast feedback)
- `feature/*`, `main` → All states build (validates everywhere)

All states share:
- ✅ Same CI scripts ([scripts/ci/](../../scripts/ci/))
- ✅ Same workflow logic ([.github/workflows/state-ci.yaml](../../.github/workflows/state-ci.yaml))
- ✅ Same codebase (in `main`)

Each state customizes:
- Infrastructure (Docker vs native)
- Versions (Node, pnpm, .NET)
- Build flags and configuration
- Environment variables
- Feature flags

---

## State Configuration Reference

### File Location

```
config/states/
├── _template.yaml    # Template for new states
├── dc.yaml           # District of Columbia
└── co.yaml           # Colorado
```

### Configuration Schema

```yaml
# State identifier (must match filename)
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

# Environment variables
environment:
  API_BASE_URL: "https://..."      # API endpoint
  features:                        # Feature flags
    multi_language: false
    advanced_search: false
    experimental_ui: false

# Deployment (optional)
deployment:
  enabled: false
  target: "production|staging"
  notify_on_success: true
  notify_on_failure: true
```

---

## How It Works

### 1. State Discovery (Branch-Based)

Workflow discovers states based on **branch name**:

```yaml
# .github/workflows/state-ci.yaml
- name: Discover state configurations
  run: |
    BRANCH="${{ github.ref_name }}"

    # Priority 1: Manual workflow_dispatch with specific state
    if [ -n "${{ github.event.inputs.state }}" ]; then
      echo "matrix={\"state\":[\"$STATE\"]}"

    # Priority 2: Branch pattern (deploy/STATE or deploy/STATE-feature)
    elif [[ "$BRANCH" =~ ^deploy/([a-z]{2})(-.*)?$ ]]; then
      STATE="${BASH_REMATCH[1]}"
      echo "matrix={\"state\":[\"$STATE\"]}"

    # Priority 3: Default (main, feature branches, PRs) = all states
    else
      STATES=$(find config/states -name "*.yaml" ...)
      echo "matrix={\"state\":$STATES}"
    fi
```

**Results**:
- `deploy/dc` → `["dc"]` (single state)
- `deploy/co-hotfix` → `["co"]` (single state)
- `main` or `feature/auth` → `["dc", "co"]` (all states)

### 2. Configuration Loading

For each state, load YAML config:

```yaml
- name: Load state configuration
  run: |
    yq eval '.infrastructure.use_docker' config/states/${{ matrix.state }}.yaml
    yq eval '.versions.node' config/states/${{ matrix.state }}.yaml
    # ... load all config values
```

### 3. Conditional Build

Based on `use_docker`, choose build path:

```yaml
# Docker path
- name: Build (Docker)
  if: steps.config.outputs.use_docker == 'true'
  run: |
    docker run --rm \
      -v ${{ github.workspace }}:/workspace \
      node:${{ steps.config.outputs.node_version }}-alpine \
      ./scripts/ci/build-frontend.sh

# Native path
- name: Build (Native)
  if: steps.config.outputs.use_docker != 'true'
  uses: actions/setup-node@v4
  with:
    node-version: ${{ steps.config.outputs.node_version }}
  run: ./scripts/ci/build-frontend.sh
```

---

## Examples

### Example 1: Docker State (DC)

```yaml
# config/states/dc.yaml
state: dc
infrastructure:
  use_docker: true
  docker:
    registry: "ghcr.io/codeforamerica"
versions:
  node: "24.x"
  pnpm: "10.x"
```

**Result**: Builds in Docker containers using node:24-alpine

### Example 2: Custom Flags

```yaml
# config/states/co.yaml
state: co
infrastructure:
  use_docker: true
build:
  frontend_flags: "--experimental-features"
  backend_flags: "--verbosity detailed"
environment:
  features:
    experimental_ui: true
```

**Result**: Builds with experimental features enabled

---

## Testing

### Local Testing with ACT

**Test all states:**
```bash
pnpm ci:test:states
```

**Test specific state:**
```bash
pnpm ci:test:state:dc
pnpm ci:test:state:co
```

**Manual trigger:**
```bash
act workflow_dispatch -W .github/workflows/state-ci.yaml --input state=dc
```

### GitHub Actions

**Automatic triggers:**
- Push to `main` → Builds **all states**
- Push to `deploy/dc` or `deploy/dc-*` → Builds **DC only**
- Push to `deploy/co` or `deploy/co-*` → Builds **CO only**
- Pull requests to `main` → Builds **all states**

**Manual trigger:**
```
Actions → State CI → Run workflow → Select state (or leave empty for all)
```

**Branch Strategy Examples:**
```bash
# Work on DC-specific feature
git checkout -b deploy/dc-new-dashboard
git push origin deploy/dc-new-dashboard
# ✅ Only DC builds in CI

# Work on CO-specific hotfix
git checkout -b deploy/co-bugfix
git push origin deploy/co-bugfix
# ✅ Only CO builds in CI

# Work on shared feature
git checkout -b feature/authentication
git push origin feature/authentication
# ✅ All states build (DC + CO)

# Merge to main
git checkout main
git merge feature/authentication
git push origin main
# ✅ All states build (DC + CO)
```

---

## Adding a New State

### Step 1: Create Configuration

```bash
# Copy template
cp config/states/_template.yaml config/states/NEW_STATE.yaml
```

### Step 2: Edit Configuration

Follow the template structure and customize:
- State identifier
- Infrastructure preferences (Docker vs native)
- Version requirements
- Build configuration
- Environment variables and feature flags

### Step 3: Test Locally

```bash
# Test the new state
pnpm ci:test:state:NEW_STATE
```

### Step 4: Commit and Push

```bash
git add config/states/NEW_STATE.yaml
git commit -m "feat: add NEW_STATE state configuration"
git push
```

**Done!** CI automatically discovers and builds the new state.

**Note:** New state config merges to `main` and becomes part of production codebase. Deployments will use only state-specific features via config.

---

## Best Practices

### 1. **Use Template**

Always start from `_template.yaml`:
```bash
cp config/states/_template.yaml config/states/NEW_STATE.yaml
```

### 2. **Test Before Committing**

```bash
# Test locally with ACT
pnpm ci:test:state:NEW_STATE
```

### 3. **Document State-Specific Requirements**

Add comments to config files:
```yaml
# State-specific infrastructure requirements
infrastructure:
  use_docker: false  # Native builds preferred
```

### 4. **Keep Versions Consistent**

Unless there's a specific reason, use same versions:
```yaml
versions:
  node: "24.x"  # Current LTS
  pnpm: "10.x"  # Latest stable
  dotnet: "10.0.x"  # Current version
```

### 5. **Feature Flags Over Code Duplication**

Use feature flags for state-specific behavior (all code lives in `main`):
```yaml
# config/states/dc.yaml
environment:
  features:
    new_dashboard: true  # DC has this

# config/states/co.yaml
environment:
  features:
    new_dashboard: false  # CO doesn't
```

```typescript
// Code in main branch
if (config.features.new_dashboard && state === 'dc') {
  return <Dashboard />;
}
```

### 6. **Branch Strategy Guidelines**

- **State-specific work** → `deploy/STATE-feature` (fast CI, single state)
- **Shared features** → `feature/name` (validates all states)
- **Keep branches short-lived** → Merge within 1-3 days
- **Update from main frequently** → `git merge main` daily

---

## Troubleshooting

### CI Fails to Discover State

**Symptom**: New state config not found

**Solution**: Ensure:
- File is in `config/states/`
- File has `.yaml` extension
- File is not named `_template.yaml`
- File is committed and pushed

### Build Fails for Specific State

**Symptom**: One state fails, others pass

**Debug**:
1. Check state config for typos
2. Test locally: `pnpm ci:test:state:STATE`
3. Check workflow logs for config values loaded
4. Verify versions are available (e.g., Node 24.x exists)

### Docker Registry Issues

**Symptom**: Docker pull fails

**Solution**: Update registry in state config:
```yaml
infrastructure:
  docker:
    registry: "YOUR_REGISTRY"
```

### Version Not Available

**Symptom**: `Node version 25.x not found`

**Solution**: Use available versions:
```bash
# Check available Node versions
docker run node:alpine cat /etc/os-release
```

Update config:
```yaml
versions:
  node: "24.x"  # Use current LTS
```

---

## Migration Guide

### From Old Workflows

**Before** (separate workflows):
```
.github/workflows/
├── ci-frontend.yaml
├── ci-backend.yaml
└── ci-docker.yaml
```

**After** (unified):
```
.github/workflows/
└── state-ci.yaml

config/states/
├── dc.yaml (Docker)
└── co.yaml (Docker)
```

**Migration steps:**
1. Create state configs from old workflows
2. Test with `pnpm ci:test:states`
3. Once verified, delete old workflows
4. Update README and docs

---

## Advanced Usage

### State-Specific Branches

```yaml
# Trigger only for state-specific branches
on:
  push:
    branches: ['deploy/dc-*', 'deploy/co-*']
```

### Conditional Deployment

```yaml
# In state config
deployment:
  enabled: true
  target: "production"

# In workflow
- name: Deploy
  if: steps.config.outputs.deployment_enabled == 'true'
  run: # Deployment steps
```

### Matrix Expansion

View full matrix:
```bash
# In workflow
- name: Show matrix
  run: echo '${{ toJson(matrix) }}'
```

---

## FAQ

**Q: Can states share configuration?**

A: Yes, use YAML anchors or create a `defaults.yaml`:
```yaml
# config/defaults.yaml
versions: &default_versions
  node: "24.x"
  pnpm: "10.x"

# config/states/dc.yaml
versions: *default_versions
```

**Q: How do I disable a state temporarily?**

A: Rename file:
```bash
mv config/states/dc.yaml config/states/dc.yaml.disabled
```

**Q: Can I test multiple states locally?**

A: Yes:
```bash
pnpm ci:test:states  # Tests all states
```

**Q: How do I add state-specific secrets?**

A: Use GitHub Environments:
```yaml
# .github/workflows/state-ci.yaml
jobs:
  build:
    environment: ${{ matrix.state }}
```

Then configure secrets per environment in GitHub.

---

## Next Steps

- [CI Setup Guide](./ci-setup.md) - Overview of CI system
- [Local CI Testing](./local-ci-testing.md) - ACT setup and usage
- [Contributing Guide](../CONTRIBUTING.md) - Development workflow
