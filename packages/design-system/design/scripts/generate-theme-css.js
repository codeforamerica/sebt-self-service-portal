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
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { LOCAL_FONTS_MAP, assertLocalFontFilesExist, extractFonts } from './generate-fonts.js'

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

/**
 * Emits @font-face rules and the --font-* variables for one state.
 *
 * The portal declares fonts here rather than through next/font because the state
 * is only known at runtime: a build-time loader would have to declare every
 * state's faces, which then cannot be preloaded without fetching typefaces the
 * process will never render. Shipping them with the per-state stylesheet keeps
 * exactly one state's faces in play, and the layout preloads them from the
 * manifest this returns. The files are already self-hosted under public/fonts
 * at stable paths, and adjustFontFallback was already off, so next/font was
 * contributing little here.
 */
export function fontCss(state, root = appRoot) {
  const tokens = JSON.parse(
    readFileSync(join(designSystemRoot, 'design', 'states', `${state}.json`), 'utf8')
  )
  const { body, heading } = extractFonts(tokens)
  // Same guard generate-fonts.js applies: a token naming a font whose file is
  // missing would otherwise emit a @font-face and a preload pointing at a 404.
  assertLocalFontFilesExist([body, heading], join(root, 'design'))
  const roles = [
    { name: body, variable: '--font-primary' },
    { name: heading && heading !== body ? heading : null, variable: '--font-heading' }
  ]

  const faces = []
  const vars = []
  const preload = []

  for (const { name, variable } of roles) {
    if (!name) continue
    const local = LOCAL_FONTS_MAP[name]
    if (!local) {
      console.warn(`   ⚠️  ${state}: "${name}" is not a locally hosted font; leaving ${variable} to fall back`)
      continue
    }
    const family = name.replace(/\b\w/g, (c) => c.toUpperCase())
    for (const src of local.src) {
      const href = src.path.replace('../public', '')
      faces.push(
        `@font-face{font-family:'${family}';src:url('${href}') format('woff2');` +
          `font-weight:${src.weight};font-style:${src.style};font-display:swap}`
      )
      preload.push(href)
    }
    vars.push(`${variable}:'${family}'`)
  }

  // --font-heading falls back to the body face for single-typeface states, so
  // layout and globals.css can reference both variables unconditionally.
  if (!vars.some((v) => v.startsWith('--font-heading')) && vars.length) {
    vars.push(`--font-heading:var(--font-primary)`)
  }

  const css = `${faces.join('')}${vars.length ? `:root{${vars.join(';')}}` : ''}`
  return { css, preload: [...new Set(preload)] }
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

  const fontManifest = {}
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

    const fonts = fontCss(state)
    const outFile = join(outDir, `theme-${state}.css`)
    writeFileSync(outFile, fonts.css + css, 'utf8')
    fontManifest[state] = fonts.preload
    console.log(
      `   ${state}: ${((fonts.css.length + css.length) / 1024).toFixed(0)} KB, ` +
        `${fonts.preload.length} font file(s) → public/themes/theme-${state}.css`
    )
  }

  // The layout preloads only the active state's faces, so it needs to know which
  // files belong to which state without importing the Sass pipeline.
  writeFileSync(
    join(appRoot, 'design', 'font-manifest.json'),
    `${JSON.stringify(fontManifest, null, 2)}\n`,
    'utf8'
  )

  // Leave the partials matching STATE so anything else reading them in this build
  // (or a developer's next `pnpm dev`) sees the state they asked for.
  writePartialsFor(activeState)
  console.log(`✅ Theme stylesheets generated; Sass partials restored to ${activeState.toUpperCase()}.`)
}

// Only auto-run when invoked as a script, so tests can import fontCss.
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  main()
}
