// The .NET API and the Next.js front ends.
//
// This module makes the API with the configuration that each state uses. Then each state
// module attaches its own environment and its own waits to that resource. Thus the
// wiring of a state stays near the resources that need it.

import { resolve } from "node:path";

import type {
  DistributedApplicationBuilder,
  NextJsAppResource,
  ProjectResource,
} from "../.aspire/modules/aspire.mjs";
import { repoRoot } from "../config.mjs";
import type { AppHostConfig } from "../config.mjs";
import type { PortalDatabase } from "./database.mjs";

export interface WebApps {
  /** The portal application. */
  web: NextJsAppResource;
  /** The public enrollment checker. Each state deploys it. */
  checker: NextJsAppResource;
}

export async function addApi(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  shared: PortalDatabase,
): Promise<ProjectResource> {
  // JwtSettings.SecretKey is empty in appsettings.json. The key is [Required], and it has
  // a minimum of 32 characters. Thus the API fails its options validation at start,
  // unless the gitignored appsettings.Development.json gives a value.
  //
  // Aspire makes this value, and this module does not write one. Thus no repository holds
  // a signing key. A token from before a restart is not valid after the restart. This is
  // not a problem on a local machine, because a token expires in 15 minutes.
  const jwtSecret = await builder.addParameterWithGeneratedValue(
    "jwt-secret",
    { minLength: 64, lower: true, upper: true, numeric: true },
    { secret: true },
  );

  // The `http` launch profile gives ASPNETCORE_ENVIRONMENT and
  // Seeding__EnableDevEndpoints. Thus this module does not set them again.
  return builder
    .addProject(
      "api",
      resolve(
        repoRoot,
        "apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj",
      ),
      { launchProfileOrOptions: "http" },
    )
    .withEnvironment("STATE", config.state)
    .withEnvironment("ConnectionStrings__DefaultConnection", shared.portalDb)
    .withEnvironment("JwtSettings__SecretKey", jwtSecret)
    // PluginAssemblyPaths is not here. The connector build capability stages the DLLs,
    // and it gives the path. Read ../capabilities/connector-build/contract.mts.
    //
    // The OTLP exporter is not here either. The telemetry capability owns it, and it
    // runs after the web applications exist, because all 3 of them export. Read
    // ../capabilities/telemetry/contract.mts.
    .waitFor(shared.portalDb);
}

export async function addWebApps(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  api: ProjectResource,
): Promise<WebApps> {
  const apiEndpoint = await api.getEndpoint("http");

  // `withPnpm` runs `pnpm dev`. Thus the `predev` hook of the package continues to make
  // the design tokens and the locale files. This is the same loop as `pnpm web:dev`.
  //
  // STATE has no prefix, and this is intentional. Next puts each NEXT_PUBLIC_* reference
  // into the build. Thus the portal keeps the state on the server, and it writes the
  // state onto `<html data-state>` for each request. Read
  // docs/adr/0023-runtime-client-config.md.
  //
  // The OTLP exporter and the browser logs are not here. Read
  // ../capabilities/telemetry/contract.mts.
  const web = await builder
    .addNextJsApp("web", resolve(repoRoot, "apps/portal/src/SEBT.Portal.Web"))
    .withPnpm()
    .withEnvironment("STATE", config.state)
    .withEnvironment("BACKEND_URL", apiEndpoint)
    .waitFor(api);

  // deploy-enrollment-checker.yaml builds and sends a static export for DC and for CO.
  // Thus the checker belongs to the graph of each state, and not to the CO graph alone.
  //
  // In a deployed environment, the checker reads window.__CHECKER_CONFIG__ from a
  // config.js. The deploy writes that file into the bucket of the checker, and that file
  // goes with a static export only. Here the checker runs `next dev`, so
  // lib/client-config.ts uses the build-time environment below.
  //
  // The other flags keep the defaults of their schema. These flags are the school field,
  // the bot protection, and the analytics keys. A developer who needs one of them can set
  // it in the .env.local of the application. Next still reads that file, because the
  // values below have the higher priority.
  //
  // NEXT_PUBLIC_API_BASE_URL is absent, and this is intentional. Without it, the browser
  // calls the /api/enrollment/* route handlers of the checker, and those handlers send
  // the request to BACKEND_URL. The static export does not include those routes. There
  // the deployed apiBaseUrl points to the portal.
  //
  // STATE and NEXT_PUBLIC_STATE go together. The next.config.ts of the checker gets
  // NEXT_PUBLIC_STATE from STATE, and it writes over the value that it received. STATE
  // alone controls the build. NEXT_PUBLIC_STATE alone would be discarded, and the build
  // would quietly give the checker of CO.
  const checker = await builder
    .addNextJsApp(
      "enrollment-checker",
      resolve(repoRoot, "apps/portal/src/SEBT.EnrollmentChecker.Web"),
    )
    .withPnpm()
    .withEnvironment("STATE", config.state)
    .withEnvironment("NEXT_PUBLIC_STATE", config.state)
    .withEnvironment("BACKEND_URL", apiEndpoint)
    .waitFor(api);

  // The portal returns CORS headers for the origin of the checker, and the checker has a
  // link back to the portal. Thus each application needs the URL of the other. An
  // endpoint reference resolves after Aspire allocates the port. Thus a reference in both
  // directions is correct.
  await web.withEnvironment(
    "ENROLLMENT_CHECKER_ORIGIN",
    await checker.getEndpoint("http"),
  );
  await checker.withEnvironment(
    "NEXT_PUBLIC_PORTAL_URL",
    await web.getEndpoint("http"),
  );

  return { web, checker };
}
