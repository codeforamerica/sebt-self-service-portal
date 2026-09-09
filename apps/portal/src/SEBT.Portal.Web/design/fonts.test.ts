import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

import { primaryFont } from './fonts'

/**
 * Contract test: the SCSS font override must reference the same CSS variable
 * that the font generator produces. A mismatch causes a FOUT (flash of unstyled
 * text) because the SCSS !important override resolves to an undefined variable,
 * falling back to the browser's default serif font.
 *
 * This caught a real bug: generate-fonts.js was updated in DC-143 to produce
 * --font-primary, but _uswds-theme-custom-styles.scss still referenced
 * --font-urbanist.
 */
describe('font variable contract', () => {
  const scssPath = resolve(__dirname, 'sass/_uswds-theme-custom-styles.scss')
  const scssContent = readFileSync(scssPath, 'utf-8')

  it('primaryFont exports a CSS variable name', () => {
    expect(primaryFont.variable).toBeDefined()
    expect(primaryFont.variable).toMatch(/^--font-/)
  })

  it('SCSS font override references the same variable as primaryFont', () => {
    const cssVariable = primaryFont.variable
    expect(scssContent).toContain(`var(${cssVariable})`)
  })

  it('SCSS font override does not reference stale font variable names', () => {
    // Guard against the specific bug: old variable names left behind after
    // the font generator is updated to produce a different variable.
    const fontVarPattern = /var\(--font-([^)]+)\)/g
    const referencedVars = [...scssContent.matchAll(fontVarPattern)].map((m) => `--font-${m[1]}`)

    for (const varName of referencedVars) {
      expect(varName).toBe(primaryFont.variable)
    }
  })
})
