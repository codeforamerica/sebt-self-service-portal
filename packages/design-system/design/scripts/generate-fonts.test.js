import { describe, expect, it } from 'vitest'
import { generateFontsTs } from './generate-fonts.js'

describe('generateFontsTs', () => {
  it('emits next/font/google for a known Google font (Urbanist)', () => {
    const out = generateFontsTs(new Set(['urbanist']), 'dc')

    expect(out).toContain("from 'next/font/google'")
    expect(out).toContain('Urbanist')
    expect(out).toContain("variable: '--font-primary'")
    expect(out).toContain('export const primaryFont')
  })

  it('emits next/font/local for Museo Slab using the locally-hosted woff2', () => {
    const out = generateFontsTs(new Set(['museo slab']), 'co')

    expect(out).toContain("from 'next/font/local'")
    expect(out).toContain('Museo_Slab_500_2-webfont.woff2')
    expect(out).toContain("weight: '500'")
    expect(out).toContain("variable: '--font-primary'")
    expect(out).toContain('export const primaryFont')
    expect(out).not.toContain("from 'next/font/google'")
  })

  it('falls back to system fonts for an unknown font', () => {
    const out = generateFontsTs(new Set(['some unknown font']), 'co')

    expect(out).toContain('export const primaryFont')
    expect(out).not.toContain("from 'next/font/google'")
    expect(out).not.toContain("from 'next/font/local'")
  })

  it('emits the empty-state stub when no fonts are declared', () => {
    const out = generateFontsTs(new Set(), 'co')

    expect(out).toContain('export const primaryFont')
    expect(out).not.toContain("from 'next/font/google'")
    expect(out).not.toContain("from 'next/font/local'")
  })
})
