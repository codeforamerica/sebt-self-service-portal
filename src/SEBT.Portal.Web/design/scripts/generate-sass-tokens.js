#!/usr/bin/env node
/**
 * Generate USWDS SASS Theme Variables from Figma Design Tokens
 *
 * Transforms state-specific design tokens into USWDS SASS variables
 * that are used at compile time to theme USWDS components.
 *
 * Usage:
 *   node scripts/generate-sass-tokens.js           # Defaults to DC
 *   STATE=co node scripts/generate-sass-tokens.js  # Colorado state
 *
 * Workflow:
 * 1. Read design/states/{state}.json (source of truth from Figma)
 * 2. Extract 'theme' object with USWDS settings
 * 3. Convert to SASS variables ($theme-color-primary: 'mint-cool-60v')
 * 4. Output to design/sass/_uswds-theme-{state}.scss
 */

import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const rootDir = join(__dirname, '..', '..')

/**
 * Map Figma token names to USWDS SASS variable names
 * USWDS expects specific variable names like $theme-color-primary
 */
const TOKEN_NAME_MAP = {
  // Color tokens - need 'color-' prefix for USWDS
  'theme-primary-family': 'theme-color-primary-family',
  'theme-primary-lightest': 'theme-color-primary-lightest',
  'theme-primary-lighter': 'theme-color-primary-lighter',
  'theme-primary-light': 'theme-color-primary-light',
  'theme-primary': 'theme-color-primary',
  'theme-primary-vivid': 'theme-color-primary-vivid',
  'theme-primary-dark': 'theme-color-primary-dark',
  'theme-primary-darker': 'theme-color-primary-darker',
  'theme-secondary-family': 'theme-color-secondary-family',
  'theme-secondary-lightest': 'theme-color-secondary-lightest',
  'theme-secondary-lighter': 'theme-color-secondary-lighter',
  'theme-secondary-light': 'theme-color-secondary-light',
  'theme-secondary': 'theme-color-secondary',
  'theme-secondary-vivid': 'theme-color-secondary-vivid',
  'theme-secondary-dark': 'theme-color-secondary-dark',
  'theme-secondary-darker': 'theme-color-secondary-darker',
  'theme-link-color': 'theme-link-color',
  'theme-focus-color': 'theme-focus-color',
  // Non-color tokens - keep as-is
  'theme-button-border-radius': 'theme-button-border-radius',
  'theme-font-type-sans': 'theme-typeface-sans',
  'theme-font-type-serif': 'theme-typeface-serif',
  'theme-font-role-heading': 'theme-font-role-heading',
  'theme-font-role-alt': 'theme-font-role-alt',
  'theme-global-paragraph-styles': 'theme-global-paragraph-styles',
  'theme-global-link-styles': 'theme-global-link-styles',
  'theme-global-content-styles': 'theme-global-content-styles',
  'theme-style-body-element': 'theme-style-body-element',
  'theme-text-measure-narrow': 'theme-text-measure-narrow',
  'theme-text-measure': 'theme-text-measure',
  'theme-text-measure-wide': 'theme-text-measure-wide'
}

/**
 * Convert token reference to USWDS token string
 * {mint-cool-60v} -> 'mint-cool-60v'
 * {gold-20v} -> 'gold-20v'
 */
function toUswdsValue(value, type) {
  if (typeof value !== 'string') return value

  // Handle token references: {mint-cool-5} -> 'mint-cool-5'
  if (value.startsWith('{') && value.endsWith('}')) {
    const tokenName = value.slice(1, -1)
    return `'${tokenName}'`
  }

  // Handle already quoted strings
  if (value.startsWith("'") && value.endsWith("'")) {
    return value
  }

  // Handle booleans
  if (type === 'boolean') {
    return value === 'true' ? 'true' : 'false'
  }

  // Handle unquoted font names - need to quote them
  if (type === 'fontFamilies' && !value.startsWith("'")) {
    return `'${value}'`
  }

  return value
}

/**
 * Process theme tokens into SASS variable declarations
 */
function processThemeTokens(themeObj) {
  const variables = []

  for (const [tokenName, tokenData] of Object.entries(themeObj)) {
    if (!tokenData || typeof tokenData !== 'object' || !('$value' in tokenData)) {
      continue
    }

    // Map to USWDS variable name
    const uswdsName = TOKEN_NAME_MAP[tokenName] || tokenName
    const sassVarName = `$${uswdsName}`
    const sassValue = toUswdsValue(tokenData.$value, tokenData.$type)

    variables.push({
      name: sassVarName,
      value: sassValue,
      type: tokenData.$type,
      description: tokenData.$description || ''
    })
  }

  return variables
}

/**
 * Format a single SASS variable declaration
 */
function formatSassVariable({ name, value, description }) {
  const lines = []
  if (description) {
    // Add description as comment, handling multi-line
    const shortDesc = description.split('\n')[0].trim()
    if (shortDesc.length <= 80) {
      lines.push(`// ${shortDesc}`)
    }
  }
  lines.push(`${name}: ${value};`)
  return lines.join('\n')
}

/**
 * Generate legacy SASS variable file content (for backward compatibility)
 */
function generateSassContent(state, variables) {
  const timestamp = new Date().toISOString()

  // Group variables by category
  const colorVars = variables.filter(v => v.name.includes('color-') || v.name.includes('link-') || v.name.includes('focus-'))
  const fontVars = variables.filter(v => v.name.includes('font-') || v.name.includes('typeface-'))
  const otherVars = variables.filter(v => !colorVars.includes(v) && !fontVars.includes(v))

  return `// ==========================================================================
// USWDS Theme Settings - ${state.toUpperCase()}
// ==========================================================================
//
// Auto-generated from Figma Tokens Studio
// Source: design/states/${state}.json
// Generated: ${timestamp}
//
// DO NOT EDIT DIRECTLY - Regenerated from design tokens during build
// ==========================================================================

// --------------------------------------------------------------------------
// Asset Path Settings (MUST be first)
// --------------------------------------------------------------------------
// Tell USWDS where fonts and images are served from (public directory)
// These are copied from node_modules/@uswds/uswds/dist/ during postinstall
// --------------------------------------------------------------------------

$theme-font-path: '/fonts';
$theme-image-path: '/img';

// --------------------------------------------------------------------------
// Color Theme Settings
// --------------------------------------------------------------------------
// These map to USWDS color tokens and control component colors
// Reference: https://designsystem.digital.gov/documentation/settings/#color-settings
// --------------------------------------------------------------------------

${colorVars.map(formatSassVariable).join('\n')}

// --------------------------------------------------------------------------
// Typography Settings
// --------------------------------------------------------------------------
// Font family, roles, and typeface configuration
// Reference: https://designsystem.digital.gov/documentation/settings/#typography-settings
// --------------------------------------------------------------------------

${fontVars.map(formatSassVariable).join('\n')}

// --------------------------------------------------------------------------
// Component & Layout Settings
// --------------------------------------------------------------------------
// Button styles, text measures, and global style flags
// Reference: https://designsystem.digital.gov/documentation/settings/#component-settings
// --------------------------------------------------------------------------

${otherVars.map(formatSassVariable).join('\n')}
`
}

/**
 * Generate USWDS settings file using @use ... with () syntax for SASS modules
 * This is required for USWDS 3.x proper theme configuration
 */
function generateSettingsContent(state, variables, timestamp) {
  // Build the settings map for @use ... with ()
  const settingsLines = []

  // Always include asset paths first
  settingsLines.push('  // Asset paths - fonts and images in public directory')
  settingsLines.push("  $theme-font-path: '/fonts',")
  settingsLines.push("  $theme-image-path: '/img',")
  settingsLines.push('')

  // Group variables
  const colorVars = variables.filter(v => v.name.includes('color-') || v.name.includes('link-') || v.name.includes('focus-'))
  const fontVars = variables.filter(v => v.name.includes('font-') || v.name.includes('typeface-'))
  const otherVars = variables.filter(v => !colorVars.includes(v) && !fontVars.includes(v))

  // Add color settings
  if (colorVars.length > 0) {
    settingsLines.push('  // Color settings (from Figma tokens)')
    for (const v of colorVars) {
      settingsLines.push(`  ${v.name}: ${v.value},`)
    }
    settingsLines.push('')
  }

  // Add font settings (skip typeface tokens as they need special handling)
  const simpleFontVars = fontVars.filter(v => !v.name.includes('typeface-'))
  if (simpleFontVars.length > 0) {
    settingsLines.push('  // Typography settings')
    for (const v of simpleFontVars) {
      settingsLines.push(`  ${v.name}: ${v.value},`)
    }
    settingsLines.push('')
  }

  // Add component settings
  if (otherVars.length > 0) {
    settingsLines.push('  // Component settings')
    for (let i = 0; i < otherVars.length; i++) {
      const v = otherVars[i]
      const isLast = i === otherVars.length - 1
      settingsLines.push(`  ${v.name}: ${v.value}${isLast ? '' : ','}`)
    }
  }

  return `// ==========================================================================
// USWDS Settings Override - ${state.toUpperCase()}
// ==========================================================================
//
// Auto-generated from Figma Tokens Studio
// Source: design/states/${state}.json
// Generated: ${timestamp}
//
// DO NOT EDIT DIRECTLY - Regenerated from design tokens during build
//
// This file configures USWDS core settings using the SASS module system.
// Settings must be configured via @use ... with () syntax for SASS modules.
// ==========================================================================

// Load uswds-core with our custom settings
// This must happen BEFORE any other USWDS module is loaded
@use "uswds-core" with (
${settingsLines.join('\n')}
);

// Re-export uswds-core so other modules can access it
@forward "uswds-core";
`
}

/**
 * Main entry point
 */
function main() {
  const state = (process.env.STATE || process.env.NEXT_PUBLIC_STATE || 'dc').toLowerCase()
  const timestamp = new Date().toISOString()

  console.log(`🎨 Generating USWDS SASS variables for ${state.toUpperCase()}...\n`)

  // Paths
  const inputPath = join(rootDir, 'design', 'states', `${state}.json`)
  const outputDir = join(rootDir, 'design', 'sass')
  const themeOutputPath = join(outputDir, `_uswds-theme-${state}.scss`)
  const settingsOutputPath = join(outputDir, '_uswds-settings.scss')

  // Check input exists
  if (!existsSync(inputPath)) {
    console.log(`⚠️  No token file found at: ${inputPath}`)
    console.log('   Skipping SASS token generation.\n')
    process.exit(0)
  }

  // Ensure output directory exists
  if (!existsSync(outputDir)) {
    mkdirSync(outputDir, { recursive: true })
  }

  // Read and parse tokens
  console.log(`📖 Reading: ${inputPath}`)
  const stateJson = JSON.parse(readFileSync(inputPath, 'utf8'))

  if (!stateJson.theme) {
    console.error(`❌ No "theme" object found in ${state}.json`)
    process.exit(1)
  }

  // Process theme tokens
  const variables = processThemeTokens(stateJson.theme)
  console.log(`✅ Extracted ${variables.length} USWDS theme variables`)

  // Generate and write legacy SASS variables file (for backward compatibility)
  const sassContent = generateSassContent(state, variables)
  writeFileSync(themeOutputPath, sassContent, 'utf8')
  console.log(`✅ Generated: ${themeOutputPath}`)

  // Generate and write USWDS settings file (for SASS module system)
  const settingsContent = generateSettingsContent(state, variables, timestamp)
  writeFileSync(settingsOutputPath, settingsContent, 'utf8')
  console.log(`✅ Generated: ${settingsOutputPath}`)

  console.log(`\n✨ ${state.toUpperCase()} SASS theme files generated successfully!\n`)
}

main()
