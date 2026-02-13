import bundleAnalyzer from '@next/bundle-analyzer'
import type { NextConfig } from 'next'
import path from 'path'

const state = process.env.STATE || 'dc'

// Bundle analyzer configuration
const withBundleAnalyzer = bundleAnalyzer({
  enabled: process.env.ANALYZE === 'true'
})

const nextConfig: NextConfig = {
  reactCompiler: true,
  env: {
    NEXT_PUBLIC_STATE: state
  },
  experimental: {
    // Use our custom sass-loader configuration instead of built-in
    turbopackUseBuiltinSass: false
  },
  /* SASS Configuration for USWDS */
  sassOptions: {
    implementation: 'sass-embedded',
    includePaths: [
      path.join(__dirname, 'design/sass'),
      path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
      path.join(__dirname, 'node_modules')
    ]
  },
  /* Turbopack configuration for USWDS SASS imports */
  turbopack: {
    rules: {
      '*.scss': {
        loaders: [
          {
            loader: 'sass-loader',
            options: {
              implementation: 'sass-embedded',
              sassOptions: {
                loadPaths: [
                  path.join(__dirname, 'design/sass'),
                  path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
                  path.join(__dirname, 'node_modules')
                ]
              }
            }
          }
        ],
        as: '*.css'
      }
    }
  },
  // Standalone output for Docker/CI deployments only (set BUILD_STANDALONE=true)
  // Local dev uses standard output so `next start` serves public/ and static/ correctly
  ...(process.env.BUILD_STANDALONE === 'true' && { output: 'standalone' as const }),
  poweredByHeader: false,
  reactStrictMode: true
}

export default withBundleAnalyzer(nextConfig)
