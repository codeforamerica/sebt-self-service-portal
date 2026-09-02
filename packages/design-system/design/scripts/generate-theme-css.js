#!/usr/bin/env node
/**
 * Compiles one USWDS stylesheet per state into <app>/public/themes/theme-{state}.css.
 *
 * Why a separate stylesheet per state rather than one file scoped by selector:
 * Sass modules are singletons, so `@use "uswds-core" with (...)` can be configured
 * exactly once per compilation — two themes cannot coexist in a single pass. Each
 * state therefore gets its own compile, and the app links the right one at runtime.
 * That also means a visitor downloads one theme, not both.
 *
 * Run from an app directory (the portal), which is where sass-embedded and USWDS
 * resolve from:
 *   node ../../../../packages/design-system/design/scripts/generate-theme-css.js
 *
 * The per-state Sass partials this depends on are produced by
 * generate-sass-tokens.js, which writes whichever state STATE names. This script
 * drives that generator once per state, compiles, then restores the partials for
 * the state named by STATE so a subsequent app build is unaffected.
 */
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, writeFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const designSystemRoot = resolve(scriptDir, '../..')

// Keep in step with generate-all-tokens.js.
const STATES = ['dc', 'co']

const appRoot = process.cwd()
const require = createRequire(join(appRoot, '/'))

function loadSass() {
  try {
    return require('sass-embedded')
  } catch {
    console.error('❌ sass-embedded not resolvable from', appRoot)
    console.error('   Run this from an app directory that depends on it (e.g. SEBT.Portal.Web).')
    process.exit(1)
  }
}

/** Regenerates the Sass partials for one state, in place. */
function writePartialsFor(state) {
  execFileSync('node', [join(scriptDir, 'generate-sass-tokens.js')], {
    env: { ...process.env, STATE: state },
    stdio: 'pipe'
  })
}

function main() {
  const activeState = (process.env.STATE || 'dc').toLowerCase()
  const sass = loadSass()

  const sassDir = join(designSystemRoot, 'design', 'sass')
  const bundle = join(sassDir, 'uswds-bundle.scss')
  const outDir = join(appRoot, 'public', 'themes')

  if (!existsSync(bundle)) {
    console.log(`⚠️  No USWDS bundle at ${bundle}; skipping theme CSS generation.`)
    process.exit(0)
  }
  mkdirSync(outDir, { recursive: true })

  const loadPaths = [
    sassDir,
    join(appRoot, 'node_modules/@uswds/uswds/packages'),
    join(appRoot, 'node_modules')
  ]

  console.log('🎨 Compiling a USWDS stylesheet per state...')
  for (const state of STATES) {
    if (!existsSync(join(designSystemRoot, 'design', 'states', `${state}.json`))) {
      console.log(`   ${state}: no token file, skipping`)
      continue
    }

    writePartialsFor(state)
    const { css } = sass.compile(bundle, {
      loadPaths,
      quietDeps: true,
      style: 'compressed',
      silenceDeprecations: ['import', 'global-builtin', 'color-functions']
    })

    const outFile = join(outDir, `theme-${state}.css`)
    writeFileSync(outFile, css, 'utf8')
    console.log(`   ${state}: ${(css.length / 1024).toFixed(0)} KB → public/themes/theme-${state}.css`)
  }

  // Leave the partials matching STATE so anything else reading them in this build
  // (or a developer's next `pnpm dev`) sees the state they asked for.
  writePartialsFor(activeState)
  console.log(`✅ Theme stylesheets generated; Sass partials restored to ${activeState.toUpperCase()}.`)
}

main()
