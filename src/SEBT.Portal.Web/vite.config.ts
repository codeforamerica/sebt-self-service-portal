import { defineConfig } from 'vite';
import { resolve } from 'path';
import { exec } from 'child_process';
import { promisify } from 'util';
import { viteStaticCopy } from 'vite-plugin-static-copy';

const execAsync = promisify(exec);

/**
 * Vite Configuration for SEBT Portal Web
 *
 * USWDS Custom Compiler Setup (following official USWDS docs):
 * https://designsystem.digital.gov/documentation/getting-started/developers/phase-two-compile/
 *
 * This replaces @uswds/compile with Vite's native capabilities:
 * 1. Sass compilation with Autoprefixer (required by USWDS)
 * 2. Static asset copying (fonts, images, JS)
 * 3. Figma token transformation
 * 4. TypeScript compilation
 * 5. Dev server with HMR
 */
export default defineConfig({
  // Build configuration
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

  // Dev server
  server: {
    port: 5173,
    strictPort: false
  },

  // CSS/Sass configuration
  css: {
    // Use Lightning CSS for faster CSS processing (5-10x faster than PostCSS)
    transformer: 'lightningcss',
    lightningcss: {
      // USWDS browser support targets
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
        // Add USWDS packages to Sass load paths
        loadPaths: [
          resolve(__dirname, 'node_modules/@uswds/uswds/packages'),
          resolve(__dirname, 'node_modules'),
          resolve(__dirname, 'sass')
        ]
      }
    }
  },

  // Plugins
  plugins: [
    // Token transformation before Sass compilation
    {
      name: 'figma-tokens',
      async buildStart() {
        console.log('🎨 Converting Figma tokens to USWDS theme variables...');
        try {
          const { stdout } = await execAsync('node scripts/tokens-to-scss.js');
          console.log(stdout);
        } catch (error) {
          this.error(`Token conversion failed: ${error.message}`);
        }
      }
    },

    // Copy USWDS static assets (required by USWDS components)
    viteStaticCopy({
      targets: [
        {
          src: 'node_modules/@uswds/uswds/dist/fonts/*',
          dest: 'fonts'
        },
        {
          src: 'node_modules/@uswds/uswds/dist/img/*',
          dest: 'img'
        },
        {
          src: 'node_modules/@uswds/uswds/dist/js/*',
          dest: 'js'
        }
      ]
    })
  ],

  // Public directory configuration
  publicDir: 'public'
});
