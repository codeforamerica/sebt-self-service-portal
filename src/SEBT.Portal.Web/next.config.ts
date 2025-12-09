import bundleAnalyzer from '@next/bundle-analyzer'
import type { NextConfig } from 'next'
import path from 'path'

const state = process.env.STATE || 'dc'

// Bundle analyzer configuration
const withBundleAnalyzer = bundleAnalyzer({
  enabled: process.env.ANALYZE === 'true'
})

// Content Security Policy for production
const ContentSecurityPolicy = `
  default-src 'self';
  script-src 'self' 'unsafe-inline' 'unsafe-eval';
  style-src 'self' 'unsafe-inline' https://fonts.googleapis.com;
  font-src 'self' https://fonts.gstatic.com;
  img-src 'self' data: https:;
  connect-src 'self';
  frame-ancestors 'none';
  base-uri 'self';
  form-action 'self';
`

const securityHeaders = [
  {
    key: 'Content-Security-Policy',
    value: ContentSecurityPolicy.replace(/\s{2,}/g, ' ').trim()
  },
  {
    key: 'X-Frame-Options',
    value: 'DENY'
  },
  {
    key: 'X-Content-Type-Options',
    value: 'nosniff'
  },
  {
    key: 'Referrer-Policy',
    value: 'strict-origin-when-cross-origin'
  },
  {
    key: 'Permissions-Policy',
    value: 'camera=(), microphone=(), geolocation=()'
  }
]

const nextConfig: NextConfig = {
  reactCompiler: true,
  env: {
    NEXT_PUBLIC_STATE: state
  },
  /* SASS Configuration for USWDS */
  sassOptions: {
    includePaths: [
      path.join(__dirname, 'design/sass'),
      path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
      path.join(__dirname, 'node_modules')
    ],
    // Only provide sass:math globally, let each file import uswds-core as needed
    additionalData: `@use "sass:math";`
  },
  /* Security Headers */
  async headers() {
    return [
      {
        source: '/:path*',
        headers: securityHeaders
      }
    ]
  },
  output: 'standalone', // For multi-state deployments
  poweredByHeader: false,
  reactStrictMode: true
}

export default withBundleAnalyzer(nextConfig)
