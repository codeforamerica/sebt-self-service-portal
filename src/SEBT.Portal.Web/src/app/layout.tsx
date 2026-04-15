import { BetaBanner } from '@/components/BetaBanner'
import { MixpanelAnalytics } from '@/components/MixpanelAnalytics'
import { primaryFont } from '@/design/fonts'
import { portalRoutes } from '@/lib/analytics-routes'
import {
  AuthProvider,
  AxeProvider,
  DataLayerProvider,
  FeatureFlagsProvider,
  I18nProvider,
  QueryProvider
} from '@/providers'
import { GoogleAnalytics } from '@next/third-parties/google'
import { getState, getStateName, SkipNav } from '@sebt/design-system'
import { Footer, Header, HelpSection } from '@sebt/design-system/client'
import type { Metadata, Viewport } from 'next'
import { headers } from 'next/headers'
import './globals.css'
import './styles.scss'

const state = getState()
const stateName = getStateName(state)

function getDefaultBaseUrl() {
  return process.env.NEXT_PUBLIC_BASE_URL ?? `https://sebt.${state}.gov`
}
const gaId = process.env.NEXT_PUBLIC_GA_ID
const mixpanelToken = process.env.NEXT_PUBLIC_MIXPANEL_TOKEN

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 5
}

export async function generateMetadata(): Promise<Metadata> {
  const h = await headers()
  const host = h.get('host')
  const proto = h.get('x-forwarded-proto') ?? 'http'
  const baseUrl = host ? `${proto}://${host}` : getDefaultBaseUrl()

  return {
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
        <DataLayerProvider
          application="sebt-portal"
          routes={portalRoutes}
        >
          <QueryProvider>
            <AuthProvider>
              <FeatureFlagsProvider>
                <I18nProvider>
                  <SkipNav />
                  <AxeProvider>
                    {/* Portal target for page-level alerts rendered above the header.
                        Currently used by AddressForm (30-char street address error).
                        If a second consumer appears, refactor to a SiteAlertContext so
                        child components call setSiteAlert() instead of using createPortal directly. */}
                    <div id="site-alerts" />
                    <BetaBanner />
                    <Header state={state} />
                    <main id="main-content">{children}</main>
                    <HelpSection state={state} />
                    <Footer state={state} />
                  </AxeProvider>
                </I18nProvider>
              </FeatureFlagsProvider>
            </AuthProvider>
          </QueryProvider>
        </DataLayerProvider>
        {/* USWDS initialization script - uses nonce for CSP compliance */}
        {/* suppressHydrationWarning: nonce changes per request, mismatch is expected */}
        <script
          src="/js/uswds-init.min.js"
          defer
          nonce={nonce}
          suppressHydrationWarning
        />
      </body>
      {/* Google Analytics - only rendered when GA_ID is configured */}
      {/* nonce is required for CSP compliance: proxy.ts enforces nonce-based strict-dynamic */}
      {gaId && (
        <GoogleAnalytics
          gaId={gaId}
          {...(nonce ? { nonce } : {})}
        />
      )}
      {/* Mixpanel - only rendered when MIXPANEL_TOKEN is configured */}
      {mixpanelToken && (
        <MixpanelAnalytics
          token={mixpanelToken}
          {...(nonce ? { nonce } : {})}
        />
      )}
    </html>
  )
}
