import { Footer, Header, SkipNav } from '@/components/layout'
import { primaryFont } from '@/design/fonts'
import { getState } from '@/lib/state'
import { AxeProvider, FeatureFlagsProvider, I18nProvider, QueryProvider } from '@/providers'
import type { Metadata, Viewport } from 'next'
import { headers } from 'next/headers'
import './globals.css'
import './styles.scss'

const state = getState()
const stateName = state === 'dc' ? 'District of Columbia' : state.toUpperCase()
const baseUrl = process.env.NEXT_PUBLIC_BASE_URL ?? `https://sebt.${state}.gov`

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 5
}

export const metadata: Metadata = {
  title: {
    default: `${stateName} SUN Bucks Self-Service Portal`,
    template: `%s | ${stateName} SUN Bucks`
  },
  description: `Apply for Summer EBT (SUN Bucks) benefits in ${stateName}. Check eligibility, track your application status, and manage your benefits online.`,
  keywords: ['SUN Bucks', 'Summer EBT', 'SEBT', 'summer meals', 'food benefits', stateName],
  authors: [{ name: `${stateName} Government` }],
  robots: {
    index: true,
    follow: true,
    googleBot: {
      index: true,
      follow: true,
      'max-video-preview': -1,
      'max-image-preview': 'large',
      'max-snippet': -1
    }
  },
  openGraph: {
    type: 'website',
    locale: 'en_US',
    url: baseUrl,
    siteName: `${stateName} SUN Bucks`,
    title: `${stateName} SUN Bucks Self-Service Portal`,
    description: `Apply for Summer EBT (SUN Bucks) benefits in ${stateName}. Check eligibility and manage your benefits online.`,
    images: [
      {
        url: `${baseUrl}/images/states/${state}/og-image.png`,
        width: 1200,
        height: 630,
        alt: `${stateName} SUN Bucks Portal`
      }
    ]
  },
  twitter: {
    card: 'summary_large_image',
    title: `${stateName} SUN Bucks Self-Service Portal`,
    description: `Apply for Summer EBT (SUN Bucks) benefits in ${stateName}.`,
    images: [`${baseUrl}/images/states/${state}/og-image.png`]
  },
  icons: {
    icon: '/favicon.ico',
    apple: '/apple-touch-icon.png'
  },
  metadataBase: new URL(baseUrl)
}

export default async function RootLayout({
  children
}: Readonly<{
  children: React.ReactNode
}>) {
  // Get nonce from proxy for CSP-compliant script loading
  const nonce = (await headers()).get('x-nonce') ?? undefined

  return (
    <html
      lang="en"
      data-state={state}
      className={`usa-js-loading ${primaryFont.variable}`}
    >
      <body>
        <QueryProvider>
          <FeatureFlagsProvider>
            <I18nProvider>
              <SkipNav />
              <AxeProvider>
                <Header state={state} />
                <main id="main-content">{children}</main>
                <Footer state={state} />
              </AxeProvider>
            </I18nProvider>
          </FeatureFlagsProvider>
        </QueryProvider>
        {/* USWDS initialization script - uses nonce for CSP compliance */}
        {/* suppressHydrationWarning: nonce changes per request, mismatch is expected */}
        <script
          src="/js/uswds-init.min.js"
          defer
          nonce={nonce}
          suppressHydrationWarning
        />
      </body>
    </html>
  )
}
