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
