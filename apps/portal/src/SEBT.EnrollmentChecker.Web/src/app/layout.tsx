// Direct subpath imports avoid the @sebt/design-system barrel export, which
// re-exports react-i18next-dependent modules. Importing from the barrel in a
// Server Component would pull react-i18next into the RSC bundle and crash.
import { CheckerShell } from '@/components/CheckerShell'
import { headingFont, primaryFont } from '@/design/fonts'
import { RuntimeAnalytics } from '@/components/RuntimeAnalytics'
import { env } from '@/lib/env'
import { buildRootMetadata } from '@/lib/metadata'
import { Providers } from '@/providers/Providers'
import { getState } from '@sebt/design-system/src/lib/state'
import type { Viewport } from 'next'
import Script from 'next/script'
import './globals.css'
import './styles.scss'

const state = getState()

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 5
}

export const metadata = buildRootMetadata(state)

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html
      lang="en"
      data-state={state}
      className={`usa-js-loading ${primaryFont.variable} ${headingFont.variable}`}
    >
      <head>
        {process.env.NEXT_PUBLIC_BUILD_SHA && (
          <meta name="build-sha" content={process.env.NEXT_PUBLIC_BUILD_SHA} />
        )}
        {/* Loaded before the app bundle so window.__CHECKER_CONFIG__ is set by the
            time any module reads it. Replaced per environment in the deployed
            bucket; the copy in public/ is an empty default. basePath-prefixed so
            it resolves under a sub-path deployment. */}
        <Script
          src={`${env.NEXT_PUBLIC_BASE_PATH}/config.js`}
          strategy="beforeInteractive"
        />
      </head>
      <body>
        <Providers>
          <CheckerShell state={state}>{children}</CheckerShell>
        </Providers>
        <script src="/js/uswds-init.min.js" defer />
      </body>
      {/* Vendor tags come from runtime config, so they are rendered on the client:
          a Server Component would freeze them into the static export. */}
      <RuntimeAnalytics />
    </html>
  )
}
