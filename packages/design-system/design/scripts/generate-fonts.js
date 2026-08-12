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
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs'
import { join, dirname, relative } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const rootDir = join(__dirname, '..', '..')
const rel = p => relative(rootDir, p)

// Map of Google Fonts available via next/font/google
// Key: lowercase font name, Value: import name from next/font/google
const GOOGLE_FONTS_MAP = {
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

// Map of locally-hosted typefaces loaded via next/font/local.
// Used for fonts that aren't on Google Fonts (e.g. exljbris's Museo Slab).
// Key: lowercase font name. Value: { variable, src } where src paths are
// relative to the generated design/fonts.ts so next/font/local can resolve them.
// Files live at <app>/public/fonts/{name}/ and must be supplied alongside this
// map entry — the generator does not bundle fonts.
const LOCAL_FONTS_MAP = {
  'museo slab': {
    variable: 'museoSlab',
    src: [
      {
        // Font Squirrel's distributed filename — preserved verbatim so the
        // file matches what's downloaded from the legitimate redistributor.
        path: '../public/fonts/museo-slab/Museo_Slab_500_2-webfont.woff2',
        weight: '500',
        style: 'normal'
      }
    ]
  },
  // Vendored from Google Fonts (OFL) to avoid a next/font/google build-time
  // fetch to fonts.gstatic.com, which is unreliable in CI. Both are variable
  // fonts, so one file covers the full weight range.
  urbanist: {
    variable: 'urbanist',
    src: [
      {
        path: '../public/fonts/urbanist/Urbanist-Variable-latin.woff2',
        weight: '400 700',
        style: 'normal'
      }
    ]
  },
  'atkinson hyperlegible': {
    variable: 'atkinsonHyperlegibleNext',
    src: [
      {
        path: '../public/fonts/atkinson-hyperlegible-next/AtkinsonHyperlegibleNext-Variable-latin.woff2',
        weight: '400 700',
        style: 'normal'
      }
    ]
  }
}

// Default font weights to load
const DEFAULT_WEIGHTS = ['400', '600', '700']

/**
 * Read the body and heading typefaces from design tokens.
 *
 * Project convention: the sans typeface is the body font and the serif typeface
 * is the heading font. Most states use a single typeface for both roles (e.g.
 * DC's Urbanist), in which case body === heading and only one font is loaded.
 *
 * @returns {{ body: string|null, heading: string|null }} lowercased family names
 */
export function extractFonts(tokensJson) {
  const theme = tokensJson?.theme
  if (!theme) {
    return { body: null, heading: null }
  }

  const read = key => {
    const value = theme[key]?.$value
    return value ? value.replace(/'/g, '').toLowerCase() : null
  }

  return {
    body: read('theme-font-type-sans'),
    heading: read('theme-font-type-serif')
  }
}

function fileHeader(state) {
  return `/**
 * Font Configuration - ${state.toUpperCase()}
 *
 * Auto-generated from design tokens.
 * Source: design/states/${state}.json
 * DO NOT EDIT DIRECTLY - Regenerate with: pnpm tokens
 *
 * Generated: ${new Date().toISOString()}
 */`
}

/**
 * Build the import line + export declaration for a single font role.
 *
 * Resolves the font name against the local then Google font maps. Unknown fonts
 * fall back to a stub object that still exposes the CSS variable, so layout code
 * can apply `<font>.variable` unconditionally.
 *
 * @returns {{ import: string|null, declaration: string }}
 */
function buildFontLoader(fontName, { variable, exportName }) {
  const localConfig = LOCAL_FONTS_MAP[fontName]
  if (localConfig) {
    const srcEntries = localConfig.src
      .map(s => `    { path: '${s.path}', weight: '${s.weight}', style: '${s.style}' }`)
      .join(',\n')

    return {
      import: "import localFont from 'next/font/local'",
      declaration: `// ${exportName} (locally-hosted): ${fontName}
// adjustFontFallback: false avoids "Failed to find font override values" for fonts not in Next.js metrics
export const ${exportName} = localFont({
  src: [
${srcEntries}
  ],
  variable: '${variable}',
  display: 'optional',
  preload: true,
  fallback: ['system-ui', 'sans-serif'],
  adjustFontFallback: false
})`
    }
  }

  const googleFontImport = GOOGLE_FONTS_MAP[fontName]
  if (googleFontImport) {
    return {
      import: `import { ${googleFontImport} } from 'next/font/google'`,
      declaration: `// ${exportName} from Figma tokens: ${fontName}
// adjustFontFallback: false avoids "Failed to find font override values" for fonts not in Next.js metrics
export const ${exportName} = ${googleFontImport}({
  subsets: ['latin'],
  weight: [${DEFAULT_WEIGHTS.map(w => `'${w}'`).join(', ')}],
  variable: '${variable}',
  display: 'optional',
  preload: true,
  fallback: ['system-ui', 'sans-serif'],
  adjustFontFallback: false
})`
    }
  }

  if (fontName) {
    console.warn(`⚠️  Font "${fontName}" not found in Google Fonts or Local Fonts mapping`)
  }
  return {
    import: null,
    declaration: `// Font "${fontName}" not available via next/font/google or next/font/local - using system fonts
export const ${exportName} = {
  variable: '${variable}',
  className: ''
}`
  }
}

/**
 * Generate the contents of design/fonts.ts.
 *
 * Always exports `primaryFont` (the body font, CSS var --font-primary). When the
 * heading typeface differs from the body, also exports `headingFont` bound to a
 * separate --font-heading var so the SCSS override can apply distinct body and
 * heading fonts. When they match (single-font states like DC), `headingFont`
 * aliases `primaryFont` — nothing is loaded twice and importers always get both.
 */
export function generateFontsTs({ body, heading }, state) {
  if (!body && !heading) {
    // No custom fonts - use system fonts only
    return `${fileHeader(state)}

// No custom fonts defined in design tokens - using system fonts
export const primaryFont = {
  variable: '--font-primary',
  className: ''
}

// Single font family - headings share the body font
export const headingFont = primaryFont
`
  }

  const bodyLoader = buildFontLoader(body, { variable: '--font-primary', exportName: 'primaryFont' })

  const imports = new Set()
  if (bodyLoader.import) {
    imports.add(bodyLoader.import)
  }

  const blocks = [bodyLoader.declaration]

  // Headings reuse the body font unless a distinct heading typeface is declared.
  if (!heading || heading === body) {
    blocks.push(`// Single font family - headings share the body font
export const headingFont = primaryFont`)
  } else {
    const headingLoader = buildFontLoader(heading, { variable: '--font-heading', exportName: 'headingFont' })
    if (headingLoader.import) {
      imports.add(headingLoader.import)
    }
    blocks.push(headingLoader.declaration)
  }

  const sections = [fileHeader(state)]
  if (imports.size) {
    sections.push([...imports].join('\n'))
  }
  sections.push(...blocks)

  return `${sections.join('\n\n')}\n`
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

    console.log(`✅ Body font: ${fonts.body ?? 'system'}; heading font: ${fonts.heading ?? 'system'}`)

    const fontsTs = generateFontsTs(fonts, state)
    mkdirSync(dirname(outputPath), { recursive: true })
    writeFileSync(outputPath, fontsTs, 'utf8')

    console.log(`✅ Generated fonts.ts for ${state.toUpperCase()}`)
    console.log(`   ${rel(outputPath)}`)

    process.exit(0)
  } catch (error) {
    console.error('❌ Font generation failed:', error.message)
    process.exit(1)
  }
}

// Only auto-run when invoked as a script, not when imported by tests.
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  main()
}
