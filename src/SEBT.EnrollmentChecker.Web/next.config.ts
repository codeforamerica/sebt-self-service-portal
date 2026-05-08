import type { NextConfig } from 'next'
import path from 'path'

const state = process.env.STATE ?? 'co'
const basePath = process.env.BASE_PATH ?? ''

// @sebt/design-system is a workspace dependency installed into this package's local node_modules.
// __dirname is src/SEBT.EnrollmentChecker.Web/
const designSystemPath = path.resolve(__dirname, 'node_modules/@sebt/design-system')

if (process.env.BUILD_STANDALONE === 'true' && process.env.BUILD_STATIC === 'true') {
  throw new Error('BUILD_STANDALONE and BUILD_STATIC are mutually exclusive')
}

const nextConfig: NextConfig = {
  reactCompiler: true,
  basePath: basePath,
  transpilePackages: ['@sebt/design-system'],
  // Note: react-i18next is NOT in serverExternalPackages here (unlike the portal).
  // Instead, layout.tsx uses direct imports from @sebt/design-system subpaths to
  // avoid pulling react-i18next into the RSC server bundle via the barrel export.
  env: {
    NEXT_PUBLIC_STATE: state,
    NEXT_PUBLIC_BASE_PATH: basePath,
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
  /* Turbopack configuration for USWDS SASS imports */
  turbopack: {
    rules: {
      '*.scss': {
        loaders: [{
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
        }],
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
  // Standalone output for Docker/SSR deployments
  ...(process.env.BUILD_STANDALONE === 'true' && { output: 'standalone' as const }),
  // Static export for S3/CloudFront SSG deployments
  ...(process.env.BUILD_STATIC === 'true' && { output: 'export' as const }),
  poweredByHeader: false,
  reactStrictMode: true
}

export default nextConfig
