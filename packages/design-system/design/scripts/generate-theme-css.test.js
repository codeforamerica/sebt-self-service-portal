import { execFileSync } from 'node:child_process'
import { cpSync, existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { compileStateTheme, fontCss, selectThemeStates } from './generate-theme-css.js'

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

  describe('with tokens from an external directory', () => {
    let externalDir

    afterEach(() => {
      vi.unstubAllEnvs()
      rmSync(externalDir, { recursive: true, force: true })
    })

    it('reads the state fonts from EXTERNAL_TOKENS_DIR_{STATE}', () => {
      externalDir = mkdtempSync(join(tmpdir(), 'theme-css-external-'))
      const coTokens = JSON.parse(readFileSync(resolve(designSystemRoot, 'design/states/co.json'), 'utf8'))
      coTokens.theme['theme-font-type-sans'].$value = 'urbanist'
      coTokens.theme['theme-font-type-serif'].$value = 'urbanist'
      writeFileSync(join(externalDir, 'co.json'), JSON.stringify(coTokens))
      vi.stubEnv('EXTERNAL_TOKENS_DIR_CO', externalDir)

      expect(fontCss('co', appRoot).css).toContain("--font-primary:'Urbanist'")
    })
  })
})

describe('selectThemeStates', () => {
  const available = (...states) => (state) => states.includes(state)

  it('compiles every state that has tokens', () => {
    expect(selectThemeStates('dc', ['dc', 'co'], available('dc', 'co'))).toEqual(['dc', 'co'])
  })

  it('skips another state whose tokens are not available', () => {
    expect(selectThemeStates('co', ['dc', 'co'], available('co'))).toEqual(['co'])
  })

  it('fails when the state being built has no tokens', () => {
    expect(() => selectThemeStates('co', ['dc', 'co'], available('dc'))).toThrow(
      /No design tokens found for CO, the state being built/
    )
  })
})

/**
 * The enrollment checker compiles from the shared generated partials while the
 * portal runs this script, so compiling a state must never rewrite them.
 */
describe('compileStateTheme', () => {
  const generatedPartials = [
    '_uswds-settings.scss',
    '_uswds-theme-semantic.scss',
    '_uswds-theme-dc.scss'
  ]
  const readPartials = (dir) =>
    generatedPartials.map((name) => readFileSync(join(dir, name), 'utf8'))
  let sharedSassDir

  afterEach(() => rmSync(sharedSassDir, { recursive: true, force: true }))

  it('compiles the requested state without touching the shared partials', () => {
    sharedSassDir = mkdtempSync(join(tmpdir(), 'theme-css-test-'))
    cpSync(resolve(designSystemRoot, 'design/sass'), sharedSassDir, {
      recursive: true
    })
    execFileSync('node', [join(scriptDir, 'generate-sass-tokens.js')], {
      env: { ...process.env, STATE: 'dc', SASS_OUTPUT_DIR: sharedSassDir },
      stdio: 'pipe'
    })
    const before = readPartials(sharedSassDir)

    let compiled
    const sass = {
      compile(bundlePath, options) {
        const workDir = dirname(bundlePath)
        compiled = {
          workDir,
          loadPaths: options.loadPaths,
          semantic: readFileSync(join(workDir, '_uswds-theme-semantic.scss'), 'utf8')
        }
        return { css: '.compiled{}' }
      }
    }

    const css = compileStateTheme('co', {
      sass,
      sassDir: sharedSassDir,
      loadPaths: ['/deps']
    })

    expect(css).toBe('.compiled{}')
    expect(compiled.semantic).toContain('Source: design/states/co.json')
    expect(compiled.workDir.startsWith(sharedSassDir)).toBe(false)
    expect(compiled.loadPaths).toEqual([compiled.workDir, '/deps'])
    expect(existsSync(compiled.workDir)).toBe(false)
    expect(readPartials(sharedSassDir)).toEqual(before)
  })
})
