// Direct subpath imports avoid the @sebt/design-system barrel export, which
// re-exports react-i18next-dependent modules. Importing from the barrel in a
// Server Component would pull react-i18next into the RSC bundle and crash.
import { primaryFont } from '@/design/fonts'
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
    <html lang="en" data-state={state} className={`usa-js-loading ${primaryFont.variable}`}>
      <head>
        {process.env.NEXT_PUBLIC_BUILD_SHA && (
          <meta name="build-sha" content={process.env.NEXT_PUBLIC_BUILD_SHA} />
        )}
        {process.env.NEXT_PUBLIC_META_PIXEL && (
           <script>
           !function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?
           n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;
           n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;
           t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,
           document,'script','https://connect.facebook.net/en_US/fbevents.js');
           fbq('init', '{process.env.NEXT_PUBLIC_META_PIXEL}');
           fbq('track', "PageView");</script>
           <noscript><img height="1" width="1" style="display:none"
           src="https://www.facebook.com/tr?id={process.env.NEXT_PUBLIC_META_PIXEL}&ev=PageView&noscript=1"
           /></noscript>
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
