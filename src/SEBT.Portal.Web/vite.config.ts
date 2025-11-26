import { defineConfig, type Plugin } from 'vite';
import { resolve } from 'path';
import { viteStaticCopy } from 'vite-plugin-static-copy';

interface PurgeCSSResult {
  css: string;
  rejectedCss?: string;
}

interface PurgeCSSClass {
  purge: (options: {
    content: string[];
    css: { raw: string }[];
    safelist?: {
      standard?: RegExp[];
      deep?: RegExp[];
      greedy?: RegExp[];
    };
    defaultExtractor?: (content: string) => string[];
  }) => Promise<PurgeCSSResult[]>;
}

interface PurgeCSSModule {
  PurgeCSS: new () => PurgeCSSClass;
}

interface OutputFile {
  type: 'asset' | 'chunk';
  source?: string | Uint8Array;
  code?: string;
}

/**
 * Vite Configuration for SEBT Portal Web
 *
 * USWDS Custom Compiler Setup (following official USWDS docs):
 * https://designsystem.digital.gov/documentation/getting-started/developers/phase-two-compile/
 *
 * This replaces @uswds/compile with Vite's native capabilities:
 * 1. Sass compilation with Lightning CSS (faster than PostCSS/Autoprefixer)
 * 2. Static asset copying (fonts, images, JS)
 * 3. TypeScript compilation
 * 4. Dev server with HMR
 * 5. PurgeCSS for production builds (removes unused USWDS CSS)
 *
 * Note: Figma token transformation runs via prebuild hook in package.json
 */

/**
 * PurgeCSS Plugin for Vite
 * Removes unused CSS from USWDS in production builds
 * Pattern-based safelisting ensures scalability as app grows
 *
 * Performance: Reduces bundle ~12% (577KB → 505KB) while maintaining
 * zero-maintenance compatibility with all current and future USWDS components
 */
function purgeCSSPlugin(): Plugin {
  let PurgeCSSConstructor: PurgeCSSModule['PurgeCSS'] | null = null;

  return {
    name: 'vite-plugin-purgecss',
    apply: 'build',
    async buildStart() {
      const module = await import('purgecss/lib/purgecss.js') as PurgeCSSModule;
      PurgeCSSConstructor = module.PurgeCSS;
    },
    async generateBundle(_options, bundle) {
      if (!PurgeCSSConstructor) {
        throw new Error('PurgeCSS not loaded');
      }

      const purgecss = new PurgeCSSConstructor();

      for (const [fileName, file] of Object.entries(bundle)) {
        const isAsset = file.type === 'asset';
        const hasSource = 'source' in file;
        const isStringSource = hasSource && typeof file.source === 'string';

        if (fileName.endsWith('.css') && isAsset && isStringSource) {
          const cssSource = file.source as string;

          const purged = await purgecss.purge({
            content: [
              resolve(__dirname, 'index.html'),
              resolve(__dirname, 'src/**/*.ts'),
              resolve(__dirname, 'src/**/*.tsx'),
              resolve(__dirname, 'src/**/*.js'),
              resolve(__dirname, 'src/**/*.jsx')
            ],
            css: [{ raw: cssSource }],
            safelist: {
              standard: [
                /^usa-/,
                /^grid-/,
                /^tablet:/,
                /^desktop:/,
                /^mobile:/,
                /^mobile-lg:/,
                /^widescreen:/,
              ],
              deep: [
                /usa-/,
              ],
              greedy: [
                /:hover/,
                /:focus/,
                /:active/,
                /:visited/,
                /:before/,
                /:after/,
              ]
            },
            defaultExtractor: (content: string): string[] => {
              const broadMatches = content.match(/[^<>"'`\s]*[^<>"'`\s:]/g) || [];
              const innerMatches = content.match(/[^<>"'`\s.()]*[^<>"'`\s.():]/g) || [];
              return [...broadMatches, ...innerMatches];
            }
          });

          if (purged && purged[0] && 'source' in file) {
            (file as OutputFile).source = purged[0].css;
          }
        }
      }
    }
  };
}

export default defineConfig({
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    sourcemap: true,
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html')
      }
    }
  },

  server: {
    port: 5173,
    strictPort: false,
    open: true
  },

  css: {
    transformer: 'lightningcss',
    lightningcss: {
      targets: {
        ie: 11,
        chrome: 90,
        firefox: 88,
        safari: 14,
        edge: 90
      }
    },
    preprocessorOptions: {
      scss: {
        loadPaths: [
          resolve(__dirname, 'node_modules/@uswds/uswds/packages'),
          resolve(__dirname, 'node_modules'),
          resolve(__dirname, 'sass')
        ]
      }
    }
  },

  plugins: [
    viteStaticCopy({
      targets: [
        // Fonts: Skipped - Using Google Fonts (Urbanist) from design tokens
        // Saves ~5.9MB by not copying unused USWDS font families
        // (Public Sans, Roboto Mono, Merriweather, Source Sans Pro)

        // Images: Copy only USWDS icon sprite (essential for USWDS components)
        // Saves ~9.9MB by skipping Material Icons (deprecated), USA Icons, hero images
        {
          src: 'node_modules/@uswds/uswds/dist/img/sprite.svg',
          dest: 'img'
        },

        // JavaScript: Copy USWDS initialization script (required for interactive components)
        {
          src: 'node_modules/@uswds/uswds/dist/js/uswds-init.min.js',
          dest: 'js'
        }
      ]
    }),
    purgeCSSPlugin()
  ],

  publicDir: 'public'
});
