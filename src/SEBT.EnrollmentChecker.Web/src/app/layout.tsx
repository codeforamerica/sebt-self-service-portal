// Direct subpath imports avoid the @sebt/design-system barrel export, which
// re-exports react-i18next-dependent modules. Importing from the barrel in a
// Server Component would pull react-i18next into the RSC bundle and crash.
import { Footer } from '@sebt/design-system/src/components/layout/Footer'
import { Header } from '@sebt/design-system/src/components/layout/Header'
import { HelpSection } from '@sebt/design-system/src/components/layout/HelpSection'
import { SkipNav } from '@sebt/design-system/src/components/layout/SkipNav'
import { getState, getStateName } from '@sebt/design-system/src/lib/state'
import type { Metadata, Viewport } from 'next'
import './globals.css'
import './styles.scss'
import { Providers } from '../providers/Providers'

const state = getState()
const stateName = getStateName(state)

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 5
}

export const metadata: Metadata = {
  title: {
    default: `${stateName} SUN Bucks Enrollment Checker`,
    template: `%s | ${stateName} SUN Bucks`
  },
  description: `Check if your child is already enrolled in Summer EBT (SUN Bucks) in ${stateName}.`,
  robots: { index: false, follow: false }
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" data-state={state} className="usa-js-loading">
      <head>
        {process.env.NEXT_PUBLIC_BUILD_SHA && (
          <meta name="build-sha" content={process.env.NEXT_PUBLIC_BUILD_SHA} />
        )}
      </head>
      <body>
        <Providers>
          <SkipNav />
          <Header state={state} />
          <main id="main-content">{children}</main>
          <HelpSection state={state} />
          <Footer state={state} />
        </Providers>
        <script src="/js/uswds-init.min.js" defer />
      </body>
    </html>
  )
}
