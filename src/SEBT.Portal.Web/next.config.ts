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
  // NOTE: @sebt/design-system is NOT in transpilePackages. Turbopack handles
  // TypeScript natively. Using transpilePackages caused the entire barrel to
  // be processed in the RSC layer, pulling in react-i18next's module-level
  // createContext() where it doesn't exist. The design-system barrel is split
  // into server-safe (index.ts) and client (client.ts) entry points instead.
  reactCompiler: true,
  env: {
    NEXT_PUBLIC_STATE: state
  },
  experimental: {
    // Use our custom sass-loader configuration instead of built-in
    turbopackUseBuiltinSass: false,
    // Tree-shake the design-system barrel so importing a single export
    // doesn't pull in unrelated modules
    optimizePackageImports: ['@sebt/design-system']
  },
  /* SASS Configuration for USWDS
   * sass-loader 16 + sass-embedded uses the modern Sass API, which honors `loadPaths` only.
   * `includePaths` is legacy-API-only and is ignored — without loadPaths, `@use "uswds-core"` fails under webpack. */
  sassOptions: {
    implementation: 'sass-embedded',
    quietDeps: true,
    loadPaths: [
      path.join(designSystemPath, 'design/sass'),
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
                quietDeps: true,
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
  // Standalone output for Docker/CI deployments only (set BUILD_STANDALONE=true)
  // Local dev uses standard output so `next start` serves public/ and static/ correctly
  ...(process.env.BUILD_STANDALONE === 'true' && { output: 'standalone' as const }),
  poweredByHeader: false,
  reactStrictMode: true
}

export default withBundleAnalyzer(nextConfig)
