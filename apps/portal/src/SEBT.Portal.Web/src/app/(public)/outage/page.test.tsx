import { render, screen } from '@testing-library/react'
import { beforeAll, describe, expect, it, vi } from 'vitest'

import enDcOutage from '@/content/locales/en/dc/outage.json'
import esDcOutage from '@/content/locales/es/dc/outage.json'

import OutagePage from './page'

// Wiring test: renders the shared OutagePageContent with the portal's real generated
// locale resources, proving the outage namespace flows through end to end. The
// component's own rendering details are covered in @sebt/design-system.

vi.mock('next/image', () => ({
  default: ({ alt, src }: { alt: string; src: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      alt={alt}
      src={src}
    />
  )
}))

beforeAll(() => {
  vi.stubEnv('NEXT_PUBLIC_STATE', 'dc')
})

describe('OutagePage', () => {
  it('renders the portal outage copy from the generated locale bundle', () => {
    render(<OutagePage />)

    // The English body1 renders twice by design: the sr-only <h1> and the visible <p>.
    expect(screen.getAllByText(enDcOutage.body1)).toHaveLength(2)
    expect(screen.getByText(esDcOutage.body1)).toBeInTheDocument()
    expect(screen.getByRole('img')).toBeInTheDocument()
    expect(screen.getAllByRole('link').length).toBeGreaterThan(0)
  })
})
