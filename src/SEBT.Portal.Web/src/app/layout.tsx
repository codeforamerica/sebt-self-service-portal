import { getState } from '@/src/lib/state'
import type { Metadata } from 'next'
import { headers } from 'next/headers'
import { Footer, Header, SkipNav } from './components'
import { primaryFont } from './fonts'
import './globals.css'
import { AxeProvider, I18nProvider } from './providers'
import './styles.scss'

const state = getState()

export const metadata: Metadata = {
  title: `${state.toUpperCase()} SEBT Self-Service Portal`,
  description: `Summer EBT Self-Service Portal for ${state.toUpperCase()} - USWDS Implementation`
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
        <I18nProvider>
          <SkipNav />
          <AxeProvider>
            <Header state={state} />
            <main id="main-content">{children}</main>
            <Footer state={state} />
          </AxeProvider>
        </I18nProvider>
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
