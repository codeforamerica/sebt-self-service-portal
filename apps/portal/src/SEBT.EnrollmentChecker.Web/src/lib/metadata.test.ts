import { describe, expect, it } from 'vitest'

import { buildRootMetadata } from './metadata'

describe('buildRootMetadata', () => {
  describe('Colorado', () => {
    const metadata = buildRootMetadata('co')

    it('titles the app "Colorado Summer EBT Enrollment Checker"', () => {
      expect(metadata.title).toEqual({
        default: 'Colorado Summer EBT Enrollment Checker',
        template: '%s | Colorado Summer EBT'
      })
    })

    it('never references "SUN Bucks" anywhere in the metadata', () => {
      expect(metadata.description).toBe(
        'Check if your child is already enrolled in Colorado Summer EBT.'
      )
      expect(JSON.stringify(metadata)).not.toContain('SUN Bucks')
    })
  })

  describe('District of Columbia', () => {
    const metadata = buildRootMetadata('dc')

    it('keeps the SUN Bucks branding in the title', () => {
      expect(metadata.title).toEqual({
        default: 'District of Columbia SUN Bucks Enrollment Checker',
        template: '%s | District of Columbia SUN Bucks'
      })
    })

    it('keeps the SUN Bucks branding in the description', () => {
      expect(metadata.description).toBe(
        'Check if your child is already enrolled in District of Columbia SUN Bucks.'
      )
    })
  })

  it('marks the checker as noindex for every state', () => {
    expect(buildRootMetadata('co').robots).toEqual({ index: false, follow: false })
    expect(buildRootMetadata('dc').robots).toEqual({ index: false, follow: false })
  })
})
