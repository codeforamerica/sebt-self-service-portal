# Design System Extraction Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract shared design tokens, USWDS sass, content pipeline, and UI components from `SEBT.Portal.Web` into a new `packages/design-system` workspace package (`@sebt/design-system`), then refactor the portal to consume it.

**Architecture:** A pnpm workspace package contains all shared artifacts: CSV content files, locale generation script, Figma design token JSONs, USWDS sass bundle, and shared React components. The portal is updated to import all of these from `@sebt/design-system` via `transpilePackages`. Generated locale JSON files remain per-app (written into each app's local `content/locales/` at build time); only the CSVs and generation script are shared.

**Tech Stack:** pnpm workspaces, TypeScript, Next.js `transpilePackages`, Node.js scripts, USWDS SASS

**Spec:** `docs/superpowers/specs/2026-03-11-enrollment-checker-web-design.md` — Section 1

> **Commit steps:** Plan documents use standard `git add` / `git commit` syntax. The `commit-commands:commit` skill is an interactive Claude Code shorthand — use it when asked interactively, but do not try to invoke it inside a plan document.

---

## File Map

### Created
- `packages/design-system/package.json` — package identity (`@sebt/design-system`)
- `packages/design-system/tsconfig.json` — TypeScript config for the package
- `packages/design-system/content/states/co.csv` — moved from `src/SEBT.Portal.Web/content/states/co.csv`
- `packages/design-system/content/states/dc.csv` — moved from `src/SEBT.Portal.Web/content/states/dc.csv`
- `packages/design-system/content/scripts/generate-locales.js` — moved + updated with `--out-dir`, `--ts-out`, `--app` CLI args
- `packages/design-system/content/scripts/generate-locales.test.js` — tests for new CLI args
- `packages/design-system/design/states/co.json` — moved from `src/SEBT.Portal.Web/design/states/co.json`
- `packages/design-system/design/states/dc.json` — moved from `src/SEBT.Portal.Web/design/states/dc.json`
- `packages/design-system/design/scripts/` — all scripts moved from `src/SEBT.Portal.Web/design/scripts/`
- `packages/design-system/design/sass/` — entire sass directory moved from `src/SEBT.Portal.Web/design/sass/`
- `packages/design-system/src/components/ui/` — moved from `src/SEBT.Portal.Web/src/components/ui/`
- `packages/design-system/src/components/ui/types.ts` — moved from portal (contains `ButtonProps`, `AlertProps`, `InputFieldProps`)
- `packages/design-system/src/components/layout/` — moved from `src/SEBT.Portal.Web/src/components/layout/`
- `packages/design-system/src/components/layout/types.ts` — moved from portal (contains `StateProps`, `HeaderProps`, `FooterProps`, etc.)
- `packages/design-system/src/components/layout/LanguageSelector/types.ts` — moved from portal
- `packages/design-system/src/providers/I18nProvider.tsx` — moved from portal
- `packages/design-system/src/providers/types.ts` — created new (contains only `I18nProviderProps`; portal's `providers/types.ts` is unchanged)
- `packages/design-system/src/lib/state.ts` — moved from portal
- `packages/design-system/src/lib/links.ts` — moved from portal
- `packages/design-system/src/lib/i18n.ts` — moved from portal
- `packages/design-system/src/index.ts` — barrel export
- `src/SEBT.Portal.Web/src/lib/i18n-init.ts` — thin portal init shim (calls `initI18n` from design system)

### Modified
- `pnpm-workspace.yaml` — add `packages/*`
- `pnpm-workspace.yaml` — add `packages/*`
- `src/SEBT.Portal.Web/package.json` — add `@sebt/design-system` dep; update script paths
- `src/SEBT.Portal.Web/next.config.ts` — add `transpilePackages`, update SASS `includePaths`
- `src/SEBT.Portal.Web/tsconfig.json` — add path alias for `@sebt/design-system`
- `src/SEBT.Portal.Web/src/app/styles.scss` — update `@forward` path
- `src/SEBT.Portal.Web/src/app/layout.tsx` — update component imports
- `src/SEBT.Portal.Web/src/test-setup.ts` — update `import '@/lib/i18n'` → `import '@/lib/i18n-init'`
- All portal files importing from `@/components/`, `@/lib/state`, `@/lib/links`, `@/lib/i18n`, `@/providers/I18nProvider`

### Deleted
- `src/SEBT.Portal.Web/content/states/co.csv` (moved)
- `src/SEBT.Portal.Web/content/states/dc.csv` (moved)
- `src/SEBT.Portal.Web/content/scripts/generate-locales.js` (moved)
- `src/SEBT.Portal.Web/design/states/` (moved)
- `src/SEBT.Portal.Web/design/scripts/` (moved)
- `src/SEBT.Portal.Web/design/sass/` (moved)
- `src/SEBT.Portal.Web/src/components/ui/` (moved, including `types.ts`)
- `src/SEBT.Portal.Web/src/components/layout/` (moved, including `types.ts` and `LanguageSelector/types.ts`)
- `src/SEBT.Portal.Web/src/providers/I18nProvider.tsx` (moved)
- `src/SEBT.Portal.Web/src/lib/state.ts` (moved)
- `src/SEBT.Portal.Web/src/lib/links.ts` (moved)
- `src/SEBT.Portal.Web/src/lib/i18n.ts` (moved)

---

## Chunk 1: Workspace Setup & Package Scaffolding

### Task 1: Activate pnpm workspace packages

**Files:**
- Modify: `pnpm-workspace.yaml`
- Create: `packages/design-system/package.json`
- Create: `packages/design-system/tsconfig.json`

- [ ] **Step 1: Add `packages/*` to workspace**

Edit `pnpm-workspace.yaml`:
```yaml
packages:
  - 'src/SEBT.Portal.Web'
  - 'packages/*'
```

- [ ] **Step 2: Create directory structure**

```bash
mkdir -p packages/design-system/content/states
mkdir -p packages/design-system/content/scripts
mkdir -p packages/design-system/design/states
mkdir -p packages/design-system/design/scripts
mkdir -p packages/design-system/design/sass
mkdir -p packages/design-system/src/components/ui
mkdir -p packages/design-system/src/components/layout/LanguageSelector
mkdir -p packages/design-system/src/providers
mkdir -p packages/design-system/src/lib
```

- [ ] **Step 3: Create `packages/design-system/package.json`**

```json
{
  "name": "@sebt/design-system",
  "version": "0.0.0",
  "private": true,
  "type": "module",
  "exports": {
    "./src/*": "./src/*",
    "./design/sass/*": "./design/sass/*",
    "./design/states/*": "./design/states/*",
    "./content/states/*": "./content/states/*"
  }
}
```

- [ ] **Step 4: Create `packages/design-system/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "skipLibCheck": true
  }
}
```

- [ ] **Step 5: Run pnpm install from repo root**

```bash
pnpm install
```

Expected: no errors.

- [ ] **Step 6: Verify workspace symlink exists**

```bash
ls node_modules/@sebt/
```

Expected: `design-system` listed.

- [ ] **Step 7: Commit**

```bash
git add pnpm-workspace.yaml packages/design-system/package.json packages/design-system/tsconfig.json
git commit -m "DC-172: Scaffold @sebt/design-system workspace package"
```

---

## Chunk 2: Content Pipeline Migration

### Task 2: Move CSVs and update generate-locales.js with CLI args

**Files:**
- Move: `src/SEBT.Portal.Web/content/states/co.csv` → `packages/design-system/content/states/co.csv`
- Move: `src/SEBT.Portal.Web/content/states/dc.csv` → `packages/design-system/content/states/dc.csv`
- Move+update: `src/SEBT.Portal.Web/content/scripts/generate-locales.js` → `packages/design-system/content/scripts/generate-locales.js`
- Create: `packages/design-system/content/scripts/generate-locales.test.js`
- Modify: `src/SEBT.Portal.Web/package.json` — update `copy:generate` script

- [ ] **Step 1: Copy the CSVs to the shared package**

```bash
cp src/SEBT.Portal.Web/content/states/co.csv packages/design-system/content/states/co.csv
cp src/SEBT.Portal.Web/content/states/dc.csv packages/design-system/content/states/dc.csv
```

- [ ] **Step 2: Copy the script to the shared package**

```bash
cp src/SEBT.Portal.Web/content/scripts/generate-locales.js packages/design-system/content/scripts/generate-locales.js
```

- [ ] **Step 3: Write a failing test for the new CLI args**

Create `packages/design-system/content/scripts/generate-locales.test.js`:

```js
#!/usr/bin/env node
/**
 * Tests for generate-locales.js --app, --out-dir, --ts-out CLI args.
 * Run: node packages/design-system/content/scripts/generate-locales.test.js
 */
import { strict as assert } from 'assert'
import { execFileSync } from 'child_process'
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'fs'
import { join } from 'path'
import { fileURLToPath } from 'url'

const __dirname = fileURLToPath(new URL('.', import.meta.url))
const script = join(__dirname, 'generate-locales.js')
const tmpDir = join(__dirname, '__test_tmp__')

function setup() {
  rmSync(tmpDir, { recursive: true, force: true })
  mkdirSync(join(tmpDir, 'states'), { recursive: true })
  mkdirSync(join(tmpDir, 'locales'), { recursive: true })
  mkdirSync(join(tmpDir, 'ts-out'), { recursive: true })

  // Minimal fixture CSV with portal-only and shared content
  const csv = [
    'Content,English,Español',
    'GLOBAL - Button Continue,Continue,Continuar',
    'S1 - Landing Page - Title,Portal Landing,Portal Landing ES',
    'S7 - Portal Dashboard - Heading,Dashboard,Panel',
  ].join('\n')
  writeFileSync(join(tmpDir, 'states', 'co.csv'), csv)
}

function teardown() {
  rmSync(tmpDir, { recursive: true, force: true })
}

setup()

// Test: --app portal includes landing and dashboard, generates to --out-dir and --ts-out
execFileSync('node', [
  script,
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', join(tmpDir, 'locales'),
  '--ts-out',  join(tmpDir, 'ts-out', 'portal-resources.ts'),
  '--app',     'portal',
], { stdio: 'inherit' })

const portalContent = readFileSync(join(tmpDir, 'ts-out', 'portal-resources.ts'), 'utf8')
assert.ok(portalContent.includes('landing'),   'portal barrel must include landing namespace')
assert.ok(portalContent.includes('dashboard'), 'portal barrel must include dashboard namespace')
assert.ok(portalContent.includes('common'),    'portal barrel must include common namespace')

// Test: --app enrollment includes common but excludes dashboard
execFileSync('node', [
  script,
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', join(tmpDir, 'locales'),
  '--ts-out',  join(tmpDir, 'ts-out', 'enrollment-resources.ts'),
  '--app',     'enrollment',
], { stdio: 'inherit' })

const enrollmentContent = readFileSync(join(tmpDir, 'ts-out', 'enrollment-resources.ts'), 'utf8')
assert.ok(enrollmentContent.includes('common'),     'enrollment barrel must include common namespace')
assert.ok(!enrollmentContent.includes('dashboard'), 'enrollment barrel must NOT include dashboard namespace')

// Test: locale JSON was written to --out-dir (not to script's own directory)
assert.ok(existsSync(join(tmpDir, 'locales', 'en', 'co', 'landing.json')), 'locale JSON must be written to --out-dir')

console.log('✅ All generate-locales CLI arg tests passed')
teardown()
```

- [ ] **Step 4: Run the test — expect failure**

```bash
node packages/design-system/content/scripts/generate-locales.test.js
```

Expected: fails — `--csv-dir`, `--out-dir`, `--ts-out`, `--app` are not yet implemented.

- [ ] **Step 5: Update `generate-locales.js` — add CLI arg parsing**

Make the following four targeted edits to `packages/design-system/content/scripts/generate-locales.js`.

**Edit A — add CLI arg parsing.** Insert immediately after the final `import` statement (before `const __filename = ...`):

```js
// Parse CLI arguments
const cliArgs = process.argv.slice(2)
function getCliArg(name) {
  const idx = cliArgs.indexOf(name)
  return idx !== -1 ? cliArgs[idx + 1] : null
}
const csvDirOverride = getCliArg('--csv-dir')
const outDirOverride = getCliArg('--out-dir')
const tsOutOverride  = getCliArg('--ts-out')
const appFilter      = getCliArg('--app')   // 'portal' | 'enrollment' | null (all)
```

**Edit B — update `CONFIG.statesDir` and `CONFIG.outputDir`.** In the `CONFIG` object, replace just these two lines:

```js
// Before:
  statesDir: join(contentDir, 'states'),
  outputDir: join(contentDir, 'locales'),

// After:
  statesDir: csvDirOverride
    ? path.resolve(process.cwd(), csvDirOverride)
    : join(contentDir, 'states'),
  outputDir: outDirOverride
    ? path.resolve(process.cwd(), outDirOverride)
    : join(contentDir, 'locales'),
```

Leave all other `CONFIG` properties (`hashFile`, `locales`, `sectionToNamespace`, `pageToNamespace`, `pageKeyPrefix`) exactly as they are.

**Edit C — add namespace-to-app mapping.** Insert immediately before the `generateResourceFile` function definition:

```js
// Which namespaces belong to which app.
// 'all' = shared, included in every app's barrel file.
const NAMESPACE_APP = {
  common:                 'all',
  landing:                'all',
  disclaimer:             'all',
  personalInfo:           'all',
  confirmInfo:            'all',
  result:                 'all',
  // Portal-specific
  login:                  'portal',
  idProofing:             'portal',
  optIn:                  'portal',
  offBoarding:            'portal',
  dashboard:              'portal',
  edit:                   'portal',
  editContactPreferences: 'portal',
  editMailingAddress:     'portal',
  stepUpDisclaimer:       'portal',
  stepUpFailure:          'portal',
  proto:                  'portal',
}

function isNamespaceForApp(namespace, app) {
  if (!app) return true
  // eslint-disable-next-line security/detect-object-injection -- namespace comes from JSON filenames, not user input
  const mapped = NAMESPACE_APP[namespace] ?? 'all'
  return mapped === 'all' || mapped === app
}
```

**Edit D — two changes inside `generateResourceFile()`.** The function currently has this line near the top:

```js
  const outputPath = join(rootDir, 'src', 'lib', 'generated-locale-resources.ts')
```

Replace that one line with:

```js
  const outputPath = tsOutOverride
    ? path.resolve(process.cwd(), tsOutOverride)
    : join(rootDir, 'src', 'lib', 'generated-locale-resources.ts')
```

Then, in the inner loop that iterates `files` (inside the triple-nested loop over `languages → states → files`), the loop body currently starts with:

```js
        const namespace = file.replace('.json', '')
```

Add a filter immediately after that line:

```js
        const namespace = file.replace('.json', '')
        if (!isNamespaceForApp(namespace, appFilter)) continue
```

**Verify the test fixture assumes correct namespace mappings.** The test uses `S1 - Landing Page - Title` which maps to namespace `landing` via `pageToNamespace['landing page']` in the existing script. And `S7 - Portal Dashboard - Heading` maps to `dashboard` via `sectionToNamespace['S7']`. Confirm these mappings exist in the script's `CONFIG` before treating the test as authoritative.

- [ ] **Step 6: Run the test — expect pass**

```bash
node packages/design-system/content/scripts/generate-locales.test.js
```

Expected: `✅ All generate-locales CLI arg tests passed`

- [ ] **Step 7: Update portal `package.json` scripts to use the new script location and args**

```json
"copy:generate": "node ../../packages/design-system/content/scripts/generate-locales.js --out-dir content/locales --ts-out src/lib/generated-locale-resources.ts --app portal",
"copy:validate": "node ../../packages/design-system/content/scripts/generate-locales.js --out-dir content/locales --ts-out src/lib/generated-locale-resources.ts --app portal --validate",
```

Update `predev` to use the new `state-banner.js` location (handled in Chunk 3). For now `predev` still calls `pnpm copy:generate` which uses the updated script path automatically.

- [ ] **Step 8: Run portal locale generation and verify output is identical**

```bash
cd src/SEBT.Portal.Web && pnpm copy:generate
```

Expected: same locale JSON files in `content/locales/`; `src/lib/generated-locale-resources.ts` regenerated with same content as before.

- [ ] **Step 9: Run portal tests**

```bash
cd src/SEBT.Portal.Web && pnpm test
```

Expected: all pass.

- [ ] **Step 10: Delete the old portal CSV and script files**

```bash
rm src/SEBT.Portal.Web/content/states/co.csv
rm src/SEBT.Portal.Web/content/states/dc.csv
rm src/SEBT.Portal.Web/content/scripts/generate-locales.js
rmdir --ignore-fail-on-non-empty src/SEBT.Portal.Web/content/scripts
rmdir --ignore-fail-on-non-empty src/SEBT.Portal.Web/content/states
```

- [ ] **Step 11: Run portal tests again to confirm nothing broken by deletion**

```bash
cd src/SEBT.Portal.Web && pnpm test
```

Expected: all pass.

- [ ] **Step 12: Commit**

```bash
git add packages/design-system/content/ src/SEBT.Portal.Web/content/ src/SEBT.Portal.Web/package.json
git commit -m "DC-172: Move content CSVs and locale script to @sebt/design-system; add --app filter"
```

---

## Chunk 3: Design Token Scripts & USWDS Sass Migration

### Task 3: Move token generation scripts and design token JSON

**Files:**
- Move: `src/SEBT.Portal.Web/design/scripts/*.js` → `packages/design-system/design/scripts/`
- Move: `src/SEBT.Portal.Web/design/states/*.json` → `packages/design-system/design/states/`
- Modify: `src/SEBT.Portal.Web/package.json` — update token script paths

- [ ] **Step 1: Copy design token scripts**

```bash
cp src/SEBT.Portal.Web/design/scripts/generate-tokens.js        packages/design-system/design/scripts/
cp src/SEBT.Portal.Web/design/scripts/generate-sass-tokens.js   packages/design-system/design/scripts/
cp src/SEBT.Portal.Web/design/scripts/generate-fonts.js         packages/design-system/design/scripts/
cp src/SEBT.Portal.Web/design/scripts/generate-all-tokens.js    packages/design-system/design/scripts/
cp src/SEBT.Portal.Web/design/scripts/generate-token-types.js   packages/design-system/design/scripts/
cp src/SEBT.Portal.Web/design/scripts/validate-tokens.js        packages/design-system/design/scripts/
cp src/SEBT.Portal.Web/design/scripts/load-env.js               packages/design-system/design/scripts/
cp src/SEBT.Portal.Web/design/scripts/state-banner.js           packages/design-system/design/scripts/
# Copy the shell script if present:
cp src/SEBT.Portal.Web/design/scripts/copy-uswds-assets.sh      packages/design-system/design/scripts/ 2>/dev/null || true
```

- [ ] **Step 2: Copy design token JSON files**

```bash
cp -r src/SEBT.Portal.Web/design/states/. packages/design-system/design/states/
```

- [ ] **Step 3: Verify moved scripts find the state JSON files**

The token scripts derive their `statesDir` using `__dirname` (which now resolves to `packages/design-system/design/scripts/`). The state JSONs are at `packages/design-system/design/states/`, which is `../states/` from `scripts/`. This relative path is unchanged from the original layout, so no edits should be needed. Confirm by running:

```bash
cd packages/design-system && STATE=co node design/scripts/generate-tokens.js
```

Expected output: `✅ Generated tokens.css` (or equivalent success line from the script) with no "Cannot find" errors.

If you see `Cannot find module '../states/co.json'` or similar, update the `statesDir` derivation inside the script: find the line that builds the path to the states directory and change it from the old portal-relative path to `join(__dirname, '../states')`.

- [ ] **Step 4: Update portal `package.json` to call scripts from new location**

**Important:** merge these changes into the existing `package.json` — do not overwrite the `copy:generate` and `copy:validate` changes made in Chunk 2. Only the `tokens*`, `postinstall`, `predev`, and `prebuild` entries change here:

```json
"tokens":      "node ../../packages/design-system/design/scripts/generate-tokens.js",
"tokens:sass": "node ../../packages/design-system/design/scripts/generate-sass-tokens.js",
"tokens:fonts":"node ../../packages/design-system/design/scripts/generate-fonts.js",
"tokens:all":  "node ../../packages/design-system/design/scripts/generate-all-tokens.js",
"postinstall": "sh ../../packages/design-system/design/scripts/copy-uswds-assets.sh",
"predev": "node ../../packages/design-system/design/scripts/state-banner.js && pnpm --silent tokens && pnpm --silent tokens:sass && pnpm --silent tokens:fonts && pnpm --silent copy:generate",
"prebuild": "node ../../packages/design-system/design/scripts/state-banner.js && pnpm --silent tokens && pnpm --silent tokens:sass && pnpm --silent tokens:fonts && pnpm --silent copy:generate"
```

The `copy:generate` and `copy:validate` scripts (updated in Chunk 2) remain unchanged.

- [ ] **Step 5: Run token generation from portal to verify it works via new path**

```bash
cd src/SEBT.Portal.Web && pnpm tokens && pnpm tokens:sass && pnpm tokens:fonts
```

Expected: `design/tokens.css`, `design/sass/_uswds-theme-*.scss`, `design/fonts.ts` regenerated successfully with same output.

- [ ] **Step 6: Delete old scripts and states directories from portal**

```bash
rm -r src/SEBT.Portal.Web/design/scripts/
rm -r src/SEBT.Portal.Web/design/states/
```

- [ ] **Step 7: Run token generation after deletion to confirm**

```bash
cd src/SEBT.Portal.Web && pnpm tokens
```

Expected: succeeds.

- [ ] **Step 8: Commit**

```bash
git add packages/design-system/design/scripts/ packages/design-system/design/states/ src/SEBT.Portal.Web/design/ src/SEBT.Portal.Web/package.json
git commit -m "DC-172: Move design token scripts and state JSONs to @sebt/design-system"
```

### Task 4: Move USWDS sass bundle and update portal config

**Files:**
- Move: `src/SEBT.Portal.Web/design/sass/` → `packages/design-system/design/sass/`
- Modify: `src/SEBT.Portal.Web/next.config.ts`
- Modify: `src/SEBT.Portal.Web/src/app/styles.scss`

- [ ] **Step 1: Copy the entire sass directory**

```bash
cp -r src/SEBT.Portal.Web/design/sass/. packages/design-system/design/sass/
```

- [ ] **Step 2: Update `src/SEBT.Portal.Web/next.config.ts` SASS include paths**

Replace the `sassOptions` and `turbopack` SASS configuration:

```typescript
// pnpm hoists workspace packages to the repo root node_modules.
// __dirname here is src/SEBT.Portal.Web/, so we go up two levels to reach the root.
const designSystemPath = path.resolve(__dirname, '../../node_modules/@sebt/design-system')

// In nextConfig:
sassOptions: {
  implementation: 'sass-embedded',
  includePaths: [
    path.join(designSystemPath, 'design/sass'),
    path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
    path.join(__dirname, 'node_modules')
  ]
},
turbopack: {
  rules: {
    '*.scss': {
      loaders: [{
        loader: 'sass-loader',
        options: {
          implementation: 'sass-embedded',
          sassOptions: {
            loadPaths: [
              path.join(designSystemPath, 'design/sass'),
              path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
              path.join(__dirname, 'node_modules')
            ]
          }
        }
      }],
      as: '*.css'
    }
  }
},
```

- [ ] **Step 3: Update `src/SEBT.Portal.Web/src/app/styles.scss`**

```scss
// ==========================================================================
// SEBT Portal Main Stylesheet
// ==========================================================================
// Imports USWDS with state-specific theme from @sebt/design-system.
// STATE env var controls which theme is compiled.
// uswds-bundle resolves via sassOptions.includePaths to:
//   node_modules/@sebt/design-system/design/sass/uswds-bundle.scss
// ==========================================================================

@forward 'uswds-bundle';
```

- [ ] **Step 4: Run portal build to verify USWDS compiles from new location**

```bash
cd src/SEBT.Portal.Web && STATE=co pnpm build
```

Expected: build succeeds, no SASS errors.

- [ ] **Step 5: Delete old sass directory from portal**

```bash
rm -r src/SEBT.Portal.Web/design/sass/
```

- [ ] **Step 6: Run build again to confirm nothing broken**

```bash
cd src/SEBT.Portal.Web && STATE=co pnpm build
```

Expected: passes.

- [ ] **Step 7: Commit**

```bash
git add packages/design-system/design/sass/ src/SEBT.Portal.Web/design/sass/ src/SEBT.Portal.Web/next.config.ts src/SEBT.Portal.Web/src/app/styles.scss
git commit -m "DC-172: Move USWDS sass to @sebt/design-system; update portal include paths"
```

---

## Chunk 4: Shared Components, Providers & Lib

### Task 5: Move shared React components and lib files to the design system

**Files:**
- Move: `src/SEBT.Portal.Web/src/components/ui/*.tsx` → `packages/design-system/src/components/ui/`
- Move: `src/SEBT.Portal.Web/src/components/layout/*.tsx` → `packages/design-system/src/components/layout/`
- Move: `src/SEBT.Portal.Web/src/providers/I18nProvider.tsx` → `packages/design-system/src/providers/`
- Move: `src/SEBT.Portal.Web/src/lib/state.ts` → `packages/design-system/src/lib/`
- Move: `src/SEBT.Portal.Web/src/lib/links.ts` → `packages/design-system/src/lib/`
- Move: `src/SEBT.Portal.Web/src/lib/i18n.ts` → `packages/design-system/src/lib/`
- Create: `packages/design-system/src/index.ts`

- [ ] **Step 1: Copy UI components (including types and tests)**

```bash
cp src/SEBT.Portal.Web/src/components/ui/types.ts              packages/design-system/src/components/ui/
cp src/SEBT.Portal.Web/src/components/ui/Button.tsx            packages/design-system/src/components/ui/
cp src/SEBT.Portal.Web/src/components/ui/Button.test.tsx       packages/design-system/src/components/ui/
cp src/SEBT.Portal.Web/src/components/ui/InputField.tsx        packages/design-system/src/components/ui/
cp src/SEBT.Portal.Web/src/components/ui/InputField.test.tsx   packages/design-system/src/components/ui/
cp src/SEBT.Portal.Web/src/components/ui/Alert.tsx             packages/design-system/src/components/ui/
cp src/SEBT.Portal.Web/src/components/ui/Alert.test.tsx        packages/design-system/src/components/ui/
cp src/SEBT.Portal.Web/src/components/ui/TextLink.tsx          packages/design-system/src/components/ui/
```

- [ ] **Step 2: Copy layout components (including types and LanguageSelector subdirectory)**

```bash
cp src/SEBT.Portal.Web/src/components/layout/types.ts       packages/design-system/src/components/layout/
cp src/SEBT.Portal.Web/src/components/layout/Header.tsx     packages/design-system/src/components/layout/
cp src/SEBT.Portal.Web/src/components/layout/Footer.tsx     packages/design-system/src/components/layout/
cp src/SEBT.Portal.Web/src/components/layout/HelpSection.tsx packages/design-system/src/components/layout/
cp src/SEBT.Portal.Web/src/components/layout/SkipNav.tsx    packages/design-system/src/components/layout/
cp -r src/SEBT.Portal.Web/src/components/layout/LanguageSelector/. packages/design-system/src/components/layout/LanguageSelector/
```

Note: `LanguageSelector/types.ts` is copied by the `-r` above.

- [ ] **Step 3: Copy providers and lib files**

```bash
cp src/SEBT.Portal.Web/src/providers/I18nProvider.tsx packages/design-system/src/providers/
cp src/SEBT.Portal.Web/src/lib/state.ts               packages/design-system/src/lib/
cp src/SEBT.Portal.Web/src/lib/links.ts               packages/design-system/src/lib/
cp src/SEBT.Portal.Web/src/lib/i18n.ts                packages/design-system/src/lib/
```

Do **not** copy `src/SEBT.Portal.Web/src/providers/types.ts` wholesale — it also contains `QueryProviderProps` and `FeatureFlagsProviderProps` which are portal-specific. Instead, create `packages/design-system/src/providers/types.ts` manually with only the shared type:

```typescript
import type { ReactNode } from 'react'

export interface I18nProviderProps {
  children: ReactNode
}
```

The portal's `src/providers/types.ts` keeps its existing content and is **not** moved or deleted.

**Important — `i18n.ts` must be refactored before the copy matters.** The current file imports `{ namespaces, stateResources }` from `./generated-locale-resources`, which is a per-app generated file that will not exist in `packages/design-system/`. See Step 3a below.

- [ ] **Step 3a: Refactor `i18n.ts` — extract `initI18n` and remove the per-app import**

The shared package can only contain the helpers and types — not the initialization call that depends on generated locale data. Refactor `packages/design-system/src/lib/i18n.ts` as follows:

**Remove** the top import:
```typescript
import { namespaces, stateResources } from './generated-locale-resources'
```

**Remove** the `i18n.use(initReactI18next).init({ ... })` call at the top of the file.

**Replace** them with an exported `initI18n` function that apps call:
```typescript
import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

export type StateResources = Record<string, Record<string, Record<string, string>>>

/**
 * Initialize i18next for SEBT apps.
 * Call this once at app startup, passing the generated locale resources.
 *
 * @param stateResources  - imported from the app's generated-locale-resources.ts
 * @param namespaces      - imported from the app's generated-locale-resources.ts
 * @param state           - current state code (e.g. 'co', 'dc')
 */
export function initI18n(
  stateResources: StateResources,
  namespaces: readonly string[],
  state: string
): void {
  const stateNames: Record<string, string> = {
    dc: 'District of Columbia',
    co: 'Colorado'
  }

  // eslint-disable-next-line security/detect-object-injection -- state is validated at build time
  const resources = stateResources[state] ?? stateResources['dc']

  i18n.use(initReactI18next).init({
    resources,
    lng: 'en',
    fallbackLng: 'en',
    defaultNS: 'common',
    ns: [...namespaces],
    interpolation: {
      escapeValue: false,
      defaultVariables: {
        state: state.toUpperCase(),
        // eslint-disable-next-line security/detect-object-injection -- state is validated at build time
        stateName: stateNames[state] ?? stateNames['dc'],
        year: new Date().getFullYear().toString()
      }
    },
    react: { useSuspense: false },
    debug: process.env.NODE_ENV === 'development'
  })
}
```

Keep all existing exports unchanged: `supportedLanguages`, `SupportedLanguage`, `changeLanguage`, `getCurrentLanguage`, `languageNames`, the default `i18n` export.

**Then update the portal** to call `initI18n` at app startup. Create `src/SEBT.Portal.Web/src/lib/i18n-init.ts`:
```typescript
import { initI18n } from '@sebt/design-system'
import { namespaces, stateResources } from './generated-locale-resources'

const state = (process.env.NEXT_PUBLIC_STATE || process.env.STATE || 'dc').toLowerCase()
initI18n(stateResources, namespaces, state)
```

Then wire in the new init shim:
- `src/SEBT.Portal.Web/src/app/layout.tsx` — **add** `import '@/lib/i18n-init'` as a new side-effect import near the top. There is no existing `@/lib/i18n` import in `layout.tsx` — the old initialization was a transitive side effect triggered when `I18nProvider.tsx` imported `i18n.ts`. After the refactor that side effect is gone, so `i18n-init.ts` must be imported explicitly at the app root.
- `src/SEBT.Portal.Web/src/test-setup.ts` — has `import '@/lib/i18n'` on line 4; replace with `import '@/lib/i18n-init'`

**Why:** `i18n.ts` in the shared package must not reference per-app generated files. Apps opt in to the initialization by calling `initI18n` with their own locale resources. Both the portal and the enrollment checker will each have their own thin `i18n-init.ts`.

Also add `initI18n` to the barrel export in `index.ts`:
```typescript
export { initI18n } from './lib/i18n'
export type { StateResources } from './lib/i18n'
```

- [ ] **Step 4: Fix `@/` path-aliased imports in all moved files**

The `@/` alias is portal-specific. After copying, several files still contain it. Search for all occurrences:

```bash
grep -rn "from '@/" packages/design-system/src/
```

Known files with `@/` imports (verified against source):
- `layout/types.ts` — imports `@/lib/i18n` and `@/lib/state`
- `layout/LanguageSelector/types.ts` — imports `@/lib/i18n` and `@/lib/state`
- `layout/LanguageSelector/constants.ts` — imports `SupportedLanguage` from `@/lib/i18n`
- `providers/I18nProvider.tsx` — imports `@/lib/i18n`
- Other layout components (Header, Footer, HelpSection, LanguageSelector) may also import from `@/lib/`; the grep will find them all

For each result, replace with a relative import:

| Old (`@/` alias) | New (relative from file's location) |
|---|---|
| `from '@/lib/state'` | `from '../../lib/state'` (from `components/layout/` or `components/ui/`) · `from '../lib/state'` (from `providers/`) · `from './state'` (within `lib/`) |
| `from '@/lib/links'` | same pattern as `state` above — depth depends on the file's directory |
| `from '@/lib/i18n'` | `from '../../lib/i18n'` (from `components/`) · `from '../lib/i18n'` (from `providers/`) · `from './i18n'` (within `lib/`) |
| `from '@/components/ui/Button'` | `from '../ui/Button'` (from `components/layout/`) |
| `from '@/components/layout/X'` | `from './X'` (within `components/layout/`) |
| `from '@/providers/I18nProvider'` | _(should not exist in components — remove)_ |

The grep output will show the exact file and line number. Count directory levels from the file's location to `src/lib/` to determine the correct `../` depth.

If any component imports from `@/env`, remove that import and update the component to receive the value as a prop, or derive it from `process.env.NEXT_PUBLIC_STATE` directly (same as `state.ts` does).

Run the search again to confirm zero `@/` imports remain:

```bash
grep -rn "from '@/" packages/design-system/src/
```

Expected: no results.

- [ ] **Step 5: Create `packages/design-system/src/index.ts`**

```typescript
// @sebt/design-system — public API

// UI primitive types (defined in types.ts, not in the component files themselves)
export type { ButtonProps, ButtonVariant, AlertProps, AlertVariant, InputFieldProps } from './components/ui/types'

// Layout component types
export type { StateProps, HeaderProps, FooterProps, HelpSectionProps, LanguageSelectorProps } from './components/layout/types'

// Provider types
export type { I18nProviderProps } from './providers/types'

// UI primitives
export { Button } from './components/ui/Button'
export { InputField } from './components/ui/InputField'
export { Alert } from './components/ui/Alert'
export { TextLink } from './components/ui/TextLink'
// TextLinkProps is defined in TextLink.tsx itself (not in ui/types.ts)
export type { TextLinkProps } from './components/ui/TextLink'

// Layout chrome
export { Header } from './components/layout/Header'
export { Footer } from './components/layout/Footer'
export { HelpSection } from './components/layout/HelpSection'
export { SkipNav } from './components/layout/SkipNav'
export { LanguageSelector } from './components/layout/LanguageSelector/LanguageSelector'

// Providers
export { I18nProvider } from './providers/I18nProvider'

// State configuration
export type { StateCode, StateConfig } from './lib/state'
export { getState, getStateConfig, getStateName, getStateAssetPath } from './lib/state'

// External links
export type { StateLinks, LinkItem } from './lib/links'
export { getStateLinks, getFooterLinks, getHelpLinks } from './lib/links'

// i18n helpers
export { initI18n } from './lib/i18n'
export type { StateResources, SupportedLanguage } from './lib/i18n'
export { changeLanguage, getCurrentLanguage, languageNames, supportedLanguages } from './lib/i18n'
```

Note: the prop types (`ButtonProps`, etc.) live in `types.ts` companion files, not in the component `.tsx` files. Export them from those `types.ts` files. Verify the exact exported names match what's in each file before trusting the list above.

- [ ] **Step 6: Create `packages/design-system/vitest.config.ts`**

The moved component tests need a vitest configuration to run. Create `packages/design-system/vitest.config.ts`:

```typescript
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: [],
  },
})
```

Also add `vitest` and `@vitejs/plugin-react` to `packages/design-system/package.json` `devDependencies` (same versions as in `src/SEBT.Portal.Web/package.json`):

```json
"devDependencies": {
  "@vitejs/plugin-react": "...",
  "vitest": "...",
  "@testing-library/react": "...",
  "@testing-library/user-event": "...",
  "jsdom": "..."
}
```

Run `pnpm install` from repo root to hoist the deps.

- [ ] **Step 7: Run shared component tests**

```bash
cd packages/design-system && npx vitest run
```

Expected: all moved component tests pass (Button, InputField, Alert). The LanguageSelector test may need its `vi.mock('@/lib/i18n')` path fixed — if so, update it to `vi.mock('../../lib/i18n')` (relative from `layout/LanguageSelector/`).

- [ ] **Step 8: Run TypeScript check on the package**

```bash
cd packages/design-system && npx tsc --noEmit
```

Expected: no type errors.

- [ ] **Step 9: Commit**

```bash
git add packages/design-system/src/ packages/design-system/vitest.config.ts packages/design-system/package.json
git commit -m "DC-172: Move shared components, providers, and lib files to @sebt/design-system"
```

---

## Chunk 5: Portal Import Refactor & Validation

### Task 6: Update portal to import from @sebt/design-system

**Files:**
- Modify: `src/SEBT.Portal.Web/package.json` — add `@sebt/design-system` dep
- Modify: `src/SEBT.Portal.Web/next.config.ts` — add `transpilePackages`
- Modify: `src/SEBT.Portal.Web/tsconfig.json` — add path alias
- All portal `.tsx`/`.ts` files with `@/components/`, `@/lib/state`, `@/lib/links`, `@/lib/i18n`, `@/providers/I18nProvider` imports

- [ ] **Step 1: Add `@sebt/design-system` to portal `package.json` dependencies**

```json
"@sebt/design-system": "workspace:*"
```

Run from repo root:

```bash
pnpm install
```

- [ ] **Step 2: Add `transpilePackages` to `src/SEBT.Portal.Web/next.config.ts`**

```typescript
const nextConfig: NextConfig = {
  transpilePackages: ['@sebt/design-system'],
  reactCompiler: true,
  // ... rest unchanged
}
```

- [ ] **Step 3: Update `src/SEBT.Portal.Web/tsconfig.json` path aliases**

Add to `compilerOptions.paths`:
```json
"@sebt/design-system": ["../../packages/design-system/src/index.ts"],
"@sebt/design-system/*": ["../../packages/design-system/*"]
```

- [ ] **Step 4: Find all portal files that need import updates**

```bash
grep -rl "from '@/components/" src/SEBT.Portal.Web/src/
grep -rl "from '@/lib/state'"  src/SEBT.Portal.Web/src/
grep -rl "from '@/lib/links'"  src/SEBT.Portal.Web/src/
grep -rl "from '@/lib/i18n'"   src/SEBT.Portal.Web/src/
grep -rl "from '@/providers/I18nProvider'" src/SEBT.Portal.Web/src/
```

- [ ] **Step 5: Update each import to use `@sebt/design-system`**

| Old import | New import |
|---|---|
| `from '@/components/ui/Button'` | `from '@sebt/design-system'` |
| `from '@/components/ui/InputField'` | `from '@sebt/design-system'` |
| `from '@/components/ui/Alert'` | `from '@sebt/design-system'` |
| `from '@/components/ui/TextLink'` | `from '@sebt/design-system'` |
| `from '@/components/layout/Header'` | `from '@sebt/design-system'` |
| `from '@/components/layout/Footer'` | `from '@sebt/design-system'` |
| `from '@/components/layout/HelpSection'` | `from '@sebt/design-system'` |
| `from '@/components/layout/SkipNav'` | `from '@sebt/design-system'` |
| `from '@/components/layout/LanguageSelector/LanguageSelector'` | `from '@sebt/design-system'` |
| `from '@/lib/state'` | `from '@sebt/design-system'` |
| `from '@/lib/links'` | `from '@sebt/design-system'` |
| `from '@/lib/i18n'` | `from '@sebt/design-system'` |
| `from '@/providers/I18nProvider'` | `from '@sebt/design-system'` |

When a file imported from multiple old paths (e.g., `@/lib/state` and `@/lib/links` in the same file), merge them into a single `from '@sebt/design-system'` import — otherwise ESLint's organize-imports plugin will flag duplicate import sources.

- [ ] **Step 6: Delete the moved source files from the portal**

```bash
rm -r src/SEBT.Portal.Web/src/components/ui/
rm -r src/SEBT.Portal.Web/src/components/layout/
rm    src/SEBT.Portal.Web/src/providers/I18nProvider.tsx
rm    src/SEBT.Portal.Web/src/lib/state.ts
rm    src/SEBT.Portal.Web/src/lib/links.ts
rm    src/SEBT.Portal.Web/src/lib/i18n.ts
```

- [ ] **Step 7: Confirm no stale `@/components` or shared lib imports remain in portal**

Run each grep separately (mixed quoting causes shell parse errors):

```bash
grep -rn "from '@/components/" src/SEBT.Portal.Web/src/
grep -rn "from '@/lib/state'" src/SEBT.Portal.Web/src/
grep -rn "from '@/lib/links'" src/SEBT.Portal.Web/src/
grep -rn "from '@/lib/i18n'" src/SEBT.Portal.Web/src/
grep -rn "from '@/providers/I18nProvider'" src/SEBT.Portal.Web/src/
```

Expected: no results from any of the five commands.

- [ ] **Step 8: Run TypeScript check on portal**

```bash
cd src/SEBT.Portal.Web && npx tsc --noEmit
```

Expected: no type errors. Fix any that appear (typically missing type exports in `index.ts`).

- [ ] **Step 9: Run portal tests**

```bash
cd src/SEBT.Portal.Web && pnpm test
```

Expected: all tests pass.

Note: shared component tests (Button.test.tsx etc.) were moved to `packages/design-system/src/` in Chunk 4 and are run by the design system's vitest config (`packages/design-system/vitest.config.ts`, added in Chunk 4 Step 6). They are no longer in the portal — `pnpm test` from `src/SEBT.Portal.Web` will not include them. Coverage is not lost; it moved to the package.

- [ ] **Step 10: Run portal production build**

```bash
cd src/SEBT.Portal.Web && STATE=co BUILD_STANDALONE=true pnpm build
```

Expected: succeeds with no errors.

- [ ] **Step 11: Commit**

```bash
git add src/SEBT.Portal.Web/ packages/design-system/
git commit -m "DC-172: Refactor portal to import shared code from @sebt/design-system"
```

---

*This plan is complete. Open a PR targeting `main`. After it merges, proceed to Plan 2: Enrollment Checker App.*
