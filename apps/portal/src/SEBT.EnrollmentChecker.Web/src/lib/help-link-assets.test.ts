/**
 * HelpSection renders each DC help link's icon from
 * /images/states/dc/icons/{icon} (HelpSection.tsx), resolved against this
 * app's public directory. If an icon referenced by getHelpLinks is missing
 * here, the help band renders broken images. CO is exempt: COHelpSection
 * renders no icons.
 */
import { existsSync } from 'node:fs'
import path from 'node:path'

import { describe, expect, it } from 'vitest'

import { getHelpLinks } from '@sebt/design-system/src/lib/links'

describe('help link icon assets', () => {
  it('ships every icon getHelpLinks references for DC', () => {
    for (const link of getHelpLinks('dc')) {
      if (!link.icon) {
        continue
      }
      const file = path.join(__dirname, '../../public/images/states/dc/icons', link.icon)
      // eslint-disable-next-line security/detect-non-literal-fs-filename -- test-only read; path is built from the design-system's static link config
      expect(existsSync(file), `${link.icon} missing from public/images/states/dc/icons`).toBe(true)
    }
  })
})
