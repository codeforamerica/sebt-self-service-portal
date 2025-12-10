import '@/design/tokens.css'
import { initAxe } from '@/src/lib/axe'
import type { Metadata } from 'next'
import { urbanist } from './fonts'
import './globals.css'

const state = process.env.NEXT_PUBLIC_STATE || 'dc'

// Initialize axe-core in development for accessibility monitoring
if (process.env.NODE_ENV === 'development') {
  initAxe()
}

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
      className={`usa-js-loading ${urbanist.variable}`}
    >
      <head>
        {/* eslint-disable-next-line @next/next/no-css-tags */}
        <link
          rel="stylesheet"
          href="/css/uswds.min.css"
        />
      </head>
      <body>
        {children}
        <script
          src="/js/uswds-init.min.js"
          defer
        />
      </body>
    </html>
  )
}
