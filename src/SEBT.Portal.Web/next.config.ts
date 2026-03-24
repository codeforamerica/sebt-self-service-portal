import bundleAnalyzer from '@next/bundle-analyzer'
import type { NextConfig } from 'next'
import path from 'path'

const state = process.env.STATE || 'dc'

// @sebt/design-system is a workspace dependency installed into this package's node_modules.
// __dirname here is src/SEBT.Portal.Web/.
const designSystemPath = path.resolve(__dirname, 'node_modules/@sebt/design-system')

// Bundle analyzer configuration
const withBundleAnalyzer = bundleAnalyzer({
  enabled: process.env.ANALYZE === 'true'
})

const nextConfig: NextConfig = {
  transpilePackages: ['@sebt/design-system'],
  // Treat react-i18next as an external server package so it's not bundled into
  // the server bundle. This prevents react-i18next's module-level createContext()
  // call from being evaluated in the React Server Components context (which does
  // not have createContext). The package is still available for client components.
  serverExternalPackages: ['react-i18next'],
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
      path.join(designSystemPath, 'design/sass'),
      path.join(__dirname, 'node_modules/@uswds/uswds/packages'),
      path.join(__dirname, 'node_modules')
    ]
  },
  /* Turbopack configuration for USWDS SASS imports and React deduplication.
   * resolveAlias ensures the design-system's React imports resolve to this
   * project's single copy, preventing "Invalid hook call" dual-instance errors.
   * (Equivalent to the webpack resolve.alias below, but for Turbopack builds.) */
  turbopack: {
    resolveAlias: {
      react: './node_modules/react',
      'react-dom': './node_modules/react-dom'
    },
    rules: {
      '*.scss': {
        loaders: [
          {
            loader: 'sass-loader',
            options: {
              implementation: 'sass-embedded',
              sassOptions: {
                loadPaths: [
                  path.join(designSystemPath, 'design/sass'),
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
  /* Webpack configuration — ensures a single React instance when @sebt/design-system
   * is processed via transpilePackages (avoids "createContext is not a function" errors
   * caused by duplicate React copies from the design-system's own node_modules). */
  webpack: (config) => {
    config.resolve.alias = {
      ...config.resolve.alias,
      react: path.resolve(__dirname, 'node_modules/react'),
      'react-dom': path.resolve(__dirname, 'node_modules/react-dom')
    }
    return config
  },
  // Standalone output for Docker/CI deployments only (set BUILD_STANDALONE=true)
  // Local dev uses standard output so `next start` serves public/ and static/ correctly
  ...(process.env.BUILD_STANDALONE === 'true' && { output: 'standalone' as const }),
  poweredByHeader: false,
  reactStrictMode: true
}

export default withBundleAnalyzer(nextConfig)
