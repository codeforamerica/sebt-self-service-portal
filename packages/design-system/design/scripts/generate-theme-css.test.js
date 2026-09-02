import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

import { fontCss } from './generate-theme-css.js'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const designSystemRoot = resolve(scriptDir, '../..')

/**
 * Contract test: the SCSS font override must reference the same CSS variables the
 * theme generator emits. A mismatch causes a FOUT — the `!important` override
 * resolves to an undefined variable and falls back to the browser's default serif.
 *
 * This caught a real bug once: the generator was updated to emit `--font-primary`
 * while the SCSS still referenced `--font-urbanist`. Both ends are still generated
 * independently, so the contract still needs guarding.
 *
 * It previously lived in the portal at design/fonts.test.ts, where vitest's
 * `src/**` include meant it never actually ran.
 */
describe('theme font contract', () => {
  const scssContent = readFileSync(
    resolve(designSystemRoot, 'design/sass/_uswds-theme-custom-styles.scss'),
    'utf-8'
  )
  // fontCss resolves font files relative to <root>/design/../public, and the
  // woff2 files are vendored in the portal rather than the design system.
  const appRoot = resolve(designSystemRoot, '../../apps/portal/src/SEBT.Portal.Web')

  for (const state of ['dc', 'co']) {
    it(`${state}: emits every font variable the SCSS references`, () => {
      const { css } = fontCss(state, appRoot)

      const referenced = new Set(
        [...scssContent.matchAll(/var\(\s*(--font-[a-z-]+)/g)].map((m) => m[1])
      )
      expect(referenced.size).toBeGreaterThan(0)
      for (const variable of referenced) {
        expect(css).toContain(`${variable}:`)
      }
    })

    it(`${state}: every preloaded file has a matching @font-face rule`, () => {
      const { css, preload } = fontCss(state, appRoot)

      expect(preload.length).toBeGreaterThan(0)
      for (const href of preload) {
        expect(css).toContain(`url('${href}')`)
      }
    })
  }
})
