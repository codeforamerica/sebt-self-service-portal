import { getState } from '@/src/lib/state'
import type { Metadata } from 'next'
import { Footer, Header } from './components'
import { primaryFont } from './fonts'
import './globals.css'
import { AxeProvider } from './providers'
import './styles.scss'

const state = getState()

export const metadata: Metadata = {
  title: `${state.toUpperCase()} SEBT Self-Service Portal`,
  description: `Summer EBT Self-Service Portal for ${state.toUpperCase()} - USWDS Implementation`
}

export default function RootLayout({
  children
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html
      lang="en"
      data-state={state}
      className={`usa-js-loading ${primaryFont.variable}`}
    >
      <body>
        <a
          className="usa-skipnav"
          href="#main-content"
        >
          Skip to main content
        </a>
        <AxeProvider>
          <Header state={state} />
          <main id="main-content">{children}</main>
          <Footer state={state} />
        </AxeProvider>
        {/* USWDS initialization script - CSP allows 'self' scripts */}
        <script
          src="/js/uswds-init.min.js"
          defer
        />
      </body>
    </html>
  )
}
