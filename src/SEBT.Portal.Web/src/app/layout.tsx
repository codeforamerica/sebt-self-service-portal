import { AppShell } from '@/components/AppShell'
import { headingFont, primaryFont } from '@/design/fonts'
import { SessionIdentityCacheSync } from '@/features/auth/components/SessionIdentityCacheSync'
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
import { AmplitudeAnalytics, MixpanelAnalytics, SiteImproveAnalytics } from '@sebt/analytics'
import {
  getPortalMetadataDescription,
  getSiteDisplayName,
  getState,
  getStateName,
  SkipNav
} from '@sebt/design-system'
import type { Metadata, Viewport } from 'next'
import { headers } from 'next/headers'
import './globals.css'
import './styles.scss'

const state = getState()
const stateName = getStateName(state)
const siteDisplayName = getSiteDisplayName(state)
const portalMetadataDescription = getPortalMetadataDescription(state)
const portalTitle = `${siteDisplayName} Self-Service Portal`

function getDefaultBaseUrl() {
  return process.env.NEXT_PUBLIC_BASE_URL ?? `https://sebt.${state}.gov`
}
const gaId = process.env.NEXT_PUBLIC_GA_ID
const mixpanelToken = process.env.NEXT_PUBLIC_MIXPANEL_TOKEN
const amplitudeApiKey = process.env.NEXT_PUBLIC_AMPLITUDE_API_KEY
const siteImproveId = process.env.NEXT_PUBLIC_SITEIMPROVE_ID

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
      default: portalTitle,
      template: `%s | ${siteDisplayName}`
    },
    description: portalMetadataDescription,
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
      siteName: siteDisplayName,
      title: portalTitle,
      description: portalMetadataDescription
    },
    twitter: {
      card: 'summary',
      title: portalTitle,
      description: portalMetadataDescription
    },
    icons: {
      icon: '/favicon.ico'
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
      className={`usa-js-loading ${primaryFont.variable} ${headingFont.variable}`}
    >
      <head>
        {/* Build SHA exposed for identifying the deployed commit per environment.
            Inlined at build time from NEXT_PUBLIC_BUILD_SHA (set to the GitHub
            commit SHA in CI); absent in local/dev builds. */}
        {process.env.NEXT_PUBLIC_BUILD_SHA && (
          <meta
            name="build-sha"
            content={process.env.NEXT_PUBLIC_BUILD_SHA}
          />
        )}
      </head>
      <body>
        <DataLayerProvider
          application="sebt-portal"
          routes={portalRoutes}
        >
          <QueryProvider>
            <AuthProvider>
              <SessionIdentityCacheSync />
              <FeatureFlagsProvider>
                <I18nProvider>
                  <SkipNav />
                  <AxeProvider>
                    {/* Portal target for page-level alerts rendered above the header.
                        Currently used by AddressForm (30-char street address error).
                        If a second consumer appears, refactor to a SiteAlertContext so
                        child components call setSiteAlert() instead of using createPortal directly. */}
                    <div id="site-alerts" />
                    <AppShell state={state}>{children}</AppShell>
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
      {/* Amplitude - only rendered when NEXT_PUBLIC_AMPLITUDE_API_KEY is configured */}
      {amplitudeApiKey && <AmplitudeAnalytics apiKey={amplitudeApiKey} />}
      {/* SiteImprove — only rendered when NEXT_PUBLIC_SITEIMPROVE_ID is configured */}
      {siteImproveId && (
        <SiteImproveAnalytics
          siteId={siteImproveId}
          {...(nonce ? { nonce } : {})}
        />
      )}
    </html>
  )
}
