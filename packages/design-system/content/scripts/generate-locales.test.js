#!/usr/bin/env node
/**
 * Tests for generate-locales.js --app, --out-dir, --ts-out, --sections CLI args
 * and per-output-dir regeneration caching.
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
    'Content,🟢 CO English Current,🟢 CO Español Current',
    'GLOBAL - Button Continue,Continue,Continuar',
    'S1 - Landing Page - Title,Portal Landing,Portal Landing ES',
    'S7 - Portal Dashboard - Heading,Dashboard,Panel',
  ].join('\n')
  writeFileSync(join(tmpDir, 'states', 'co.csv'), csv)
}

function run(args) {
  execFileSync('node', [script, ...args], { stdio: 'inherit' })
}

function teardown() {
  rmSync(tmpDir, { recursive: true, force: true })
}

setup()

// Test: --app portal includes landing and dashboard, generates to --out-dir and --ts-out
run([
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', join(tmpDir, 'locales'),
  '--ts-out',  join(tmpDir, 'ts-out', 'portal-resources.ts'),
  '--app',     'portal',
])

const portalContent = readFileSync(join(tmpDir, 'ts-out', 'portal-resources.ts'), 'utf8')
assert.ok(portalContent.includes('landing'),   'portal barrel must include landing namespace')
assert.ok(portalContent.includes('dashboard'), 'portal barrel must include dashboard namespace')
assert.ok(portalContent.includes('common'),    'portal barrel must include common namespace')

// Test: --app enrollment includes common but excludes dashboard
run([
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', join(tmpDir, 'locales'),
  '--ts-out',  join(tmpDir, 'ts-out', 'enrollment-resources.ts'),
  '--app',     'enrollment',
])

const enrollmentContent = readFileSync(join(tmpDir, 'ts-out', 'enrollment-resources.ts'), 'utf8')
assert.ok(enrollmentContent.includes('common'),     'enrollment barrel must include common namespace')
assert.ok(!enrollmentContent.includes('dashboard'), 'enrollment barrel must NOT include dashboard namespace')

// Test: locale JSON was written to --out-dir (not to script's own directory)
assert.ok(existsSync(join(tmpDir, 'locales', 'en', 'co', 'landing.json')), 'locale JSON must be written to --out-dir')

// Test: new CSV column headers ("Variable Name/Key", "🟢 CO English Current", "🟢 CO Español Current")
setup()
const csvNewHeaders = [
  'Variable Name/Key,🟢 CO English Current,⚪ SOURCE English,🟢 CO Español Current,⚪ SOURCE Español,Notes',
  'GLOBAL - Button Continue,Continue,,Continuar,,',
  'S1 - Landing Page - Title,New Header Landing,,New Header Landing ES,,',
].join('\n')
writeFileSync(join(tmpDir, 'states', 'co.csv'), csvNewHeaders)

run([
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', join(tmpDir, 'locales'),
  '--ts-out',  join(tmpDir, 'ts-out', 'new-header-resources.ts'),
  '--app',     'portal',
])

const newHeaderLanding = JSON.parse(readFileSync(join(tmpDir, 'locales', 'en', 'co', 'landing.json'), 'utf8'))
assert.equal(newHeaderLanding.title, 'New Header Landing', 'must parse content from "Variable Name/Key" column header')

const newHeaderCommon = JSON.parse(readFileSync(join(tmpDir, 'locales', 'es', 'co', 'common.json'), 'utf8'))
assert.equal(newHeaderCommon.buttonContinue, 'Continuar', 'must parse Spanish from state-prefixed Español column')

// Test: S11 error/maintenance page namespaces are classified per app
setup()
const csvS11 = [
  'Content,🟢 CO English Current,🟢 CO Español Current',
  'GLOBAL - Button Continue,Continue,Continuar',
  'S11 - 404 Portal - Title,Portal 404,Portal 404 ES',
  'S11 - Maintenance Portal - Title,Portal Maintenance,Portal Maintenance ES',
  'S11 - 404 Enrollment Checker - Title,Checker 404,Checker 404 ES',
  'S11 - Maintenance Enrollment Checker - Title,Checker Maintenance,Checker Maintenance ES',
].join('\n')
writeFileSync(join(tmpDir, 'states', 'co.csv'), csvS11)

run([
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', join(tmpDir, 'locales'),
  '--ts-out',  join(tmpDir, 'ts-out', 's11-portal-resources.ts'),
  '--app',     'portal',
])

const s11Portal = readFileSync(join(tmpDir, 'ts-out', 's11-portal-resources.ts'), 'utf8')
assert.ok(s11Portal.includes('404Portal'),                    'portal barrel must include 404Portal namespace')
assert.ok(s11Portal.includes('maintenancePortal'),            'portal barrel must include maintenancePortal namespace')
assert.ok(!s11Portal.includes('404EnrollmentChecker'),        'portal barrel must NOT include 404EnrollmentChecker namespace')
assert.ok(!s11Portal.includes('maintenanceEnrollmentChecker'),'portal barrel must NOT include maintenanceEnrollmentChecker namespace')

run([
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', join(tmpDir, 'locales-s11-enrollment'),
  '--ts-out',  join(tmpDir, 'ts-out', 's11-enrollment-resources.ts'),
  '--app',     'enrollment',
])

const s11Enrollment = readFileSync(join(tmpDir, 'ts-out', 's11-enrollment-resources.ts'), 'utf8')
assert.ok(s11Enrollment.includes('404EnrollmentChecker'),         'enrollment barrel must include 404EnrollmentChecker namespace')
assert.ok(s11Enrollment.includes('maintenanceEnrollmentChecker'), 'enrollment barrel must include maintenanceEnrollmentChecker namespace')
assert.ok(!s11Enrollment.includes('404Portal'),                   'enrollment barrel must NOT include 404Portal namespace')
assert.ok(!s11Enrollment.includes('maintenancePortal'),           'enrollment barrel must NOT include maintenancePortal namespace')

// Test: changing --sections with an unchanged CSV must regenerate (cache keys on CLI args)
setup()
run([
  '--csv-dir',  join(tmpDir, 'states'),
  '--out-dir',  join(tmpDir, 'locales'),
  '--ts-out',   join(tmpDir, 'ts-out', 'sections-s1.ts'),
  '--app',      'portal',
  '--sections', 'S1',
])
assert.ok(existsSync(join(tmpDir, 'locales', 'en', 'co', 'landing.json')),   'S1-only run must emit landing.json')
assert.ok(!existsSync(join(tmpDir, 'locales', 'en', 'co', 'dashboard.json')), 'S1-only run must not emit dashboard.json')

run([
  '--csv-dir',  join(tmpDir, 'states'),
  '--out-dir',  join(tmpDir, 'locales'),
  '--ts-out',   join(tmpDir, 'ts-out', 'sections-s1-s7.ts'),
  '--app',      'portal',
  '--sections', 'S1,S7',
])
assert.ok(
  existsSync(join(tmpDir, 'locales', 'en', 'co', 'dashboard.json')),
  'widening --sections with an unchanged CSV must regenerate and emit dashboard.json'
)

// Test: each --out-dir caches independently (second app is not skipped after the first regenerates)
setup()
const outA = join(tmpDir, 'locales-app-a')
const outB = join(tmpDir, 'locales-app-b')
const appArgs = (outDir, tsOut) => [
  '--csv-dir', join(tmpDir, 'states'),
  '--out-dir', outDir,
  '--ts-out',  join(tmpDir, 'ts-out', tsOut),
  '--app',     'portal',
]
run(appArgs(outA, 'cache-a1.ts'))
run(appArgs(outB, 'cache-b1.ts'))

const csvV2 = [
  'Content,🟢 CO English Current,🟢 CO Español Current',
  'GLOBAL - Button Continue,Continue,Continuar',
  'S1 - Landing Page - Title,Updated Landing,Updated Landing ES',
].join('\n')
writeFileSync(join(tmpDir, 'states', 'co.csv'), csvV2)

run(appArgs(outA, 'cache-a2.ts'))
run(appArgs(outB, 'cache-b2.ts'))

const landingB = JSON.parse(readFileSync(join(outB, 'en', 'co', 'landing.json'), 'utf8'))
assert.equal(
  landingB.title,
  'Updated Landing',
  'second out-dir must pick up the CSV change even though the first out-dir already regenerated'
)

console.log('✅ All generate-locales CLI arg tests passed')
teardown()
