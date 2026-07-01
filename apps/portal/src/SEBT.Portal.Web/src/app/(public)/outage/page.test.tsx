import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { OutagePageContent } from '@/features/outage/components/OutagePageContent'

vi.mock('@/features/outage/getOutageMessages', () => ({
  getOutageMessages: () => [
    {
      language: 'en',
      body1: 'We are down for maintenance and will be back up shortly.',
      body2: 'Try waiting a few hours and come back to this page.'
    },
    {
      language: 'es',
      body1: 'Estamos en mantenimiento y volveremos en breve.',
      body2: 'Le rogamos que espere unas horas y vuelva a esta página.'
    }
  ],
  getOutageFooterCopy: () => [
    {
      language: 'en',
      prefix: 'For more information, visit'
    },
    {
      language: 'es',
      prefix: 'Para más información, visite'
    }
  ]
}))

vi.mock('@sebt/design-system', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@sebt/design-system')>()
  return {
    ...actual,
    getState: () => 'dc',
    getStateAssetPath: (_state: string, asset: string) => `/images/states/dc/${asset}`,
    getSiteDisplayName: () => 'District of Columbia SUN Bucks',
    getStateLinks: () => ({
      help: {
        sebtMainSite: 'https://sunbucks.dc.gov/page/contact-us',
        helpDeskEmail: 'mailto:help@example.com'
      }
    })
  }
})

vi.mock('next/image', () => ({
  default: ({ alt, src }: { alt: string; src: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      alt={alt}
      src={src}
    />
  )
}))

describe('OutagePageContent', () => {
  it('renders stacked multilingual maintenance copy, logo, and footer link', () => {
    render(<OutagePageContent />)

    expect(
      screen.getByRole('heading', {
        level: 1,
        name: 'We are down for maintenance and will be back up shortly.'
      })
    ).toBeInTheDocument()
    expect(screen.getByText('Estamos en mantenimiento y volveremos en breve.')).toBeInTheDocument()
    expect(
      screen.getByText('Try waiting a few hours and come back to this page.')
    ).toBeInTheDocument()
    expect(
      screen.getByText('Le rogamos que espere unas horas y vuelva a esta página.')
    ).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'District of Columbia SUN Bucks' })).toBeInTheDocument()

    const footerLinks = screen.getAllByRole('link', {
      name: /sunbucks\.dc\.gov\/page\/contact-us/i
    })
    expect(footerLinks).toHaveLength(2)
    footerLinks.forEach((link) => {
      expect(link).toHaveAttribute('href', 'https://sunbucks.dc.gov/page/contact-us')
      expect(link).toHaveAttribute('target', '_blank')
    })
    expect(screen.getByText(/For more information, visit/i)).toBeInTheDocument()
  })
})
