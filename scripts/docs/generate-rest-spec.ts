/**
 * Stages the two generated files the REST reference page needs: the API's OpenAPI
 * document, and the RapiDoc bundle that renders it. Both land in `docs/docfx/rest/`
 * and are git-ignored, so the page always describes the branch it was built from.
 *
 * The document is produced by an xUnit test rather than by Swashbuckle's CLI. Both
 * `dotnet swagger tofile` and the build-time `GetDocument` tool start the app through
 * `HostFactoryResolver`, which runs `Program.Main` far enough to hit the plugin
 * registration; that throws without `PluginAssemblyPaths`, and supplying one makes the
 * tool load the plugin directory into its own assembly load context, where
 * `System.Composition.Runtime` collides with the copy it already holds.
 * `PortalWebApplicationFactory` already configures the host correctly for the
 * integration tests, so the export rides on that instead of re-solving it.
 *
 * The bundle is copied from `node_modules` rather than loaded from a CDN, so the site
 * builds and renders offline and ships nothing the lockfile does not pin.
 */
import { execFileSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { endpointEntries } from './postprocess-search-index.ts';

/** Test that writes the document. Narrow filter: the rest of the suite is not needed here. */
const EXPORT_TEST = 'FullyQualifiedName~OpenApiDocumentExportTests';

const TEST_PROJECT = 'apps/portal/test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj';
const RAPIDOC_BUNDLE = 'node_modules/rapidoc/dist/rapidoc-min.js';
const OUTPUT_DIR = 'docs/docfx/rest';
const SPEC_NAME = 'portal.openapi.json';

function main(): void {
  const repoRoot = resolve(fileURLToPath(import.meta.url), '../../..');
  const outputDir = join(repoRoot, OUTPUT_DIR);
  const specPath = join(outputDir, SPEC_NAME);

  mkdirSync(outputDir, { recursive: true });

  console.log('REST spec: generating the OpenAPI document from the test host...');
  execFileSync(
    'dotnet',
    ['test', join(repoRoot, TEST_PROJECT), '--filter', EXPORT_TEST, '--nologo', '--verbosity', 'quiet'],
    { cwd: repoRoot, stdio: 'inherit', env: { ...process.env, SEBT_OPENAPI_OUTPUT: specPath } },
  );

  if (!existsSync(specPath)) {
    throw new Error(
      `The export test passed but wrote nothing to ${specPath}. It only writes when SEBT_OPENAPI_OUTPUT is set, ` +
        'so check that the variable reached the test host.',
    );
  }

  const bundleSource = join(repoRoot, RAPIDOC_BUNDLE);
  if (!existsSync(bundleSource)) {
    throw new Error(`${RAPIDOC_BUNDLE} is missing. Run \`pnpm install\` before building the docs.`);
  }
  copyFileSync(bundleSource, join(outputDir, 'rapidoc-min.js'));

  // Reported so a spec that generates but comes out empty is visible in the build log.
  // Counted with the same function the search index uses, so the two never disagree.
  const operations = Object.keys(endpointEntries(JSON.parse(readFileSync(specPath, 'utf8')), '')).length;
  console.log(`REST spec: wrote ${SPEC_NAME} (${operations} operations) and staged the RapiDoc bundle.`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main();
}
