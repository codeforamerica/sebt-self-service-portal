import { mkdtempSync, mkdirSync, writeFileSync } from 'fs'
import { tmpdir } from 'os'
import { join } from 'path'
import { describe, expect, it } from 'vitest'
import { assertLocalFontFilesExist, extractFonts, generateFontsTs } from './generate-fonts.js'

describe('extractFonts', () => {
  it('maps the sans typeface to body and the serif typeface to heading', () => {
    const result = extractFonts({
      theme: {
        'theme-font-type-sans': { $value: 'Atkinson Hyperlegible' },
        'theme-font-type-serif': { $value: 'Museo Slab' }
      }
    })

    expect(result).toEqual({ body: 'atkinson hyperlegible', heading: 'museo slab' })
  })

  it('returns equal body and heading when one typeface fills both roles', () => {
    const result = extractFonts({
      theme: {
        'theme-font-type-sans': { $value: 'Urbanist' },
        'theme-font-type-serif': { $value: 'Urbanist' }
      }
    })

    expect(result).toEqual({ body: 'urbanist', heading: 'urbanist' })
  })

  it('returns nulls when no typefaces are declared', () => {
    expect(extractFonts({ theme: {} })).toEqual({ body: null, heading: null })
    expect(extractFonts({})).toEqual({ body: null, heading: null })
  })
})

describe('generateFontsTs', () => {
  it('emits next/font/local for the vendored Urbanist body font bound to --font-primary', () => {
    const out = generateFontsTs({ body: 'urbanist', heading: 'urbanist' }, 'dc')

    // Vendored locally (see LOCAL_FONTS_MAP) rather than fetched via next/font/google,
    // since the build-time fetch to fonts.gstatic.com is unreliable in CI.
    expect(out).toContain("from 'next/font/local'")
    expect(out).toContain('Urbanist-Variable.woff2')
    expect(out).toContain("variable: '--font-primary'")
    expect(out).toContain('export const primaryFont')
  })

  it('aliases headingFont to primaryFont when body and heading share a typeface', () => {
    const out = generateFontsTs({ body: 'urbanist', heading: 'urbanist' }, 'dc')

    expect(out).toContain('export const headingFont = primaryFont')
    expect(out).not.toContain("variable: '--font-heading'")
  })

  it('emits a separate heading loader bound to --font-heading when body and heading differ', () => {
    const out = generateFontsTs({ body: 'atkinson hyperlegible', heading: 'museo slab' }, 'co')

    // Body: Atkinson Hyperlegible Next via vendored next/font/local, bound to --font-primary
    expect(out).toContain("from 'next/font/local'")
    expect(out).toContain('AtkinsonHyperlegibleNext-Variable.woff2')
    expect(out).toContain("variable: '--font-primary'")
    expect(out).toContain('export const primaryFont')

    // Heading: Museo Slab via next/font/local bound to --font-heading
    expect(out).toContain('Museo_Slab_500_2-webfont.woff2')
    expect(out).toContain("variable: '--font-heading'")
    expect(out).toContain('export const headingFont')
    expect(out).not.toContain('export const headingFont = primaryFont')
  })

  it('imports next/font/local exactly once when both body and heading resolve to local fonts', () => {
    const out = generateFontsTs({ body: 'atkinson hyperlegible', heading: 'museo slab' }, 'co')

    expect(out.match(/from 'next\/font\/local'/g)).toHaveLength(1)
    expect(out).not.toContain("from 'next/font/google'")
  })

  it('falls back to system fonts but still exports both fonts for an unknown typeface', () => {
    const out = generateFontsTs({ body: 'some unknown font', heading: 'some unknown font' }, 'co')

    expect(out).toContain('export const primaryFont')
    expect(out).toContain('export const headingFont')
    expect(out).not.toContain("from 'next/font/google'")
    expect(out).not.toContain("from 'next/font/local'")
  })

  it('emits the empty-state stub for both fonts when no typefaces are declared', () => {
    const out = generateFontsTs({ body: null, heading: null }, 'co')

    expect(out).toContain('export const primaryFont')
    expect(out).toContain('export const headingFont')
    expect(out).not.toContain("from 'next/font/google'")
    expect(out).not.toContain("from 'next/font/local'")
  })
})

describe('assertLocalFontFilesExist', () => {
  it('does not throw when the referenced local font file exists', () => {
    const root = mkdtempSync(join(tmpdir(), 'generate-fonts-test-'))
    mkdirSync(join(root, 'public', 'fonts', 'urbanist'), { recursive: true })
    writeFileSync(join(root, 'public', 'fonts', 'urbanist', 'Urbanist-Variable.woff2'), '')

    expect(() => assertLocalFontFilesExist(['urbanist'], join(root, 'design'))).not.toThrow()
  })

  it('throws a clear error when a local font file is missing', () => {
    const root = mkdtempSync(join(tmpdir(), 'generate-fonts-test-'))

    expect(() => assertLocalFontFilesExist(['urbanist'], join(root, 'design'))).toThrow(
      /Urbanist-Variable\.woff2/
    )
  })

  it('ignores null/unmapped font names', () => {
    const root = mkdtempSync(join(tmpdir(), 'generate-fonts-test-'))

    expect(() => assertLocalFontFilesExist([null, 'some unknown font'], join(root, 'design'))).not.toThrow()
  })
})
