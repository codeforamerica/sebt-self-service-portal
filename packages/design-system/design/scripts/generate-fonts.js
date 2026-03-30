#!/usr/bin/env node
/**
 * Generate State-Specific Font Configuration for Next.js
 *
 * Reads font families from design tokens and generates fonts.ts
 * that uses next/font/google for optimized font loading.
 *
 * Usage:
 *   node scripts/generate-fonts.js           # Defaults to DC
 *   STATE=co node scripts/generate-fonts.js  # Colorado state
 *
 * Workflow:
 * 1. Read design/states/{state}.json
 * 2. Extract font families from theme-font-type-sans and theme-font-type-serif
 * 3. Generate design/fonts.ts with proper next/font/google imports
 */

import './load-env.js'
import { readFileSync, writeFileSync, existsSync } from 'fs'
import { join, dirname, relative } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const rootDir = join(__dirname, '..', '..')
const rel = p => relative(rootDir, p)

// Map of Google Fonts available via next/font/google
// Key: lowercase font name, Value: import name from next/font/google
const GOOGLE_FONTS_MAP = {
  urbanist: 'Urbanist',
  'atkinson hyperlegible': 'Atkinson_Hyperlegible_Next',
  'public sans': 'Public_Sans',
  roboto: 'Roboto',
  'open sans': 'Open_Sans',
  lato: 'Lato',
  montserrat: 'Montserrat',
  raleway: 'Raleway',
  poppins: 'Poppins',
  inter: 'Inter',
  'work sans': 'Work_Sans',
  nunito: 'Nunito',
  'source sans pro': 'Source_Sans_3',
  merriweather: 'Merriweather'
}

// Default font weights to load
const DEFAULT_WEIGHTS = ['400', '600', '700']

function extractFonts(tokensJson) {
  const fonts = new Set()

  if (!tokensJson.theme) {
    return fonts
  }

  const theme = tokensJson.theme

  if (theme['theme-font-type-sans']?.$value) {
    const fontName = theme['theme-font-type-sans'].$value.replace(/'/g, '').toLowerCase()
    fonts.add(fontName)
  }

  if (theme['theme-font-type-serif']?.$value) {
    const fontName = theme['theme-font-type-serif'].$value.replace(/'/g, '').toLowerCase()
    fonts.add(fontName)
  }

  return fonts
}

function generateFontsTs(fonts, state) {
  const fontArray = Array.from(fonts)

  if (fontArray.length === 0) {
    // No custom fonts - use system fonts only
    return `/**
 * Font Configuration - ${state.toUpperCase()}
 *
 * Auto-generated from design tokens.
 * Source: design/states/${state}.json
 * DO NOT EDIT DIRECTLY - Regenerate with: pnpm tokens
 *
 * Generated: ${new Date().toISOString()}
 */

// No custom fonts defined in design tokens - using system fonts
export const primaryFont = {
  variable: '--font-primary',
  className: ''
}
`
  }

  // Get primary font (first one)
  const primaryFontName = fontArray[0]
  const googleFontImport = GOOGLE_FONTS_MAP[primaryFontName]

  if (!googleFontImport) {
    console.warn(`⚠️  Font "${primaryFontName}" not found in Google Fonts mapping`)
    return `/**
 * Font Configuration - ${state.toUpperCase()}
 *
 * Auto-generated from design tokens.
 * Source: design/states/${state}.json
 * DO NOT EDIT DIRECTLY - Regenerate with: pnpm tokens
 *
 * Generated: ${new Date().toISOString()}
 */

// Font "${primaryFontName}" not available via next/font/google
// Using system fonts as fallback
export const primaryFont = {
  variable: '--font-primary',
  className: ''
}
`
  }

  // Generate the variable name (lowercase, no spaces)
  const variableName = primaryFontName.replace(/\s+/g, '')

  return `/**
 * Font Configuration - ${state.toUpperCase()}
 *
 * Auto-generated from design tokens.
 * Source: design/states/${state}.json
 * DO NOT EDIT DIRECTLY - Regenerate with: pnpm tokens
 *
 * Generated: ${new Date().toISOString()}
 */

import { ${googleFontImport} } from 'next/font/google'

// Primary font from Figma tokens: ${primaryFontName}
// adjustFontFallback: false avoids "Failed to find font override values" for fonts not in Next.js metrics
export const ${variableName} = ${googleFontImport}({
  subsets: ['latin'],
  weight: [${DEFAULT_WEIGHTS.map(w => `'${w}'`).join(', ')}],
  variable: '--font-primary',
  display: 'optional',
  preload: true,
  fallback: ['system-ui', 'sans-serif'],
  adjustFontFallback: false
})

// Export as primaryFont for consistent usage
export const primaryFont = ${variableName}
`
}

function main() {
  try {
    const state = (process.env.STATE || process.env.NEXT_PUBLIC_STATE || 'dc').toLowerCase()
    const tokensPath = join(rootDir, 'design', 'states', `${state}.json`)
    // Write to caller's working directory (e.g. src/SEBT.Portal.Web/design/fonts.ts)
    // so the Next.js @/ path alias resolves correctly at build time
    const outputPath = join(process.cwd(), 'design', 'fonts.ts')

    console.log(`🔤 Generating fonts.ts for ${state.toUpperCase()}...`)

    if (!existsSync(tokensPath)) {
      console.log(`⚠️  No token file found at: ${rel(tokensPath)}`)
      console.log('   Skipping font generation.')
      process.exit(0)
    }

    const tokensJson = JSON.parse(readFileSync(tokensPath, 'utf8'))
    const fonts = extractFonts(tokensJson)

    console.log(`✅ Found ${fonts.size} font(s): ${Array.from(fonts).join(', ')}`)

    const fontsTs = generateFontsTs(fonts, state)
    writeFileSync(outputPath, fontsTs, 'utf8')

    console.log(`✅ Generated fonts.ts for ${state.toUpperCase()}`)
    console.log(`   ${rel(outputPath)}`)

    process.exit(0)
  } catch (error) {
    console.error('❌ Font generation failed:', error.message)
    process.exit(1)
  }
}

main()
