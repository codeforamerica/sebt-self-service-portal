// The .NET API and the Next.js front ends.
//
// The API is created here with the configuration both states share; each state module
// then attaches its own environment and waits to the returned resource, keeping
// state-specific wiring next to the resources that motivate it.

import { resolve } from "node:path";

import type {
  DistributedApplicationBuilder,
  NextJsAppResource,
  ProjectResource,
} from "../.aspire/modules/aspire.mjs";
import { repoRoot } from "../config.mjs";
import type { AppHostConfig } from "../config.mjs";
import type { SharedResources } from "./shared.mjs";

export interface WebApps {
  /** The portal itself. */
  web: NextJsAppResource;
  /** The public enrollment checker, which every state deploys. */
  checker: NextJsAppResource;
}

export async function addApi(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  shared: SharedResources,
): Promise<ProjectResource> {
  // The `http` launch profile supplies ASPNETCORE_ENVIRONMENT and
  // Seeding__EnableDevEndpoints, so they are not repeated here.
  const api = await builder
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
    .withOtlpExporter()
    .waitFor(shared.portalDb);

  // withOtlpExporter sets the standard OTEL_EXPORTER_OTLP_ENDPOINT, but
  // OpenTelemetrySetup binds OtlpExporterOptions from the `Otel:OtlpExporter` config
  // section, and appsettings.json sets that to the old Jaeger address. Config wins, so
  // without this override traces and metrics are exported to a port nothing listens on.
  const otlpEndpoint = process.env.ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL;
  if (otlpEndpoint) {
    await api.withEnvironment("Otel__OtlpExporter__Endpoint", otlpEndpoint);
  }

  return api;
}

export async function addWebApps(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  api: ProjectResource,
): Promise<WebApps> {
  const apiEndpoint = await api.getEndpoint("http");

  // withPnpm runs `pnpm dev`, so the package's own predev hook still generates design
  // tokens and locale files — the same inner loop as `pnpm web:dev`.
  //
  // STATE is unprefixed on purpose: Next inlines every NEXT_PUBLIC_* reference at build
  // time, so the portal keeps the state server-side and stamps it onto <html data-state>
  // per request. See docs/adr/0023-runtime-client-config.md.
  const web = await builder
    .addNextJsApp("web", resolve(repoRoot, "apps/portal/src/SEBT.Portal.Web"))
    .withPnpm()
    .withEnvironment("STATE", config.state)
    .withEnvironment("BACKEND_URL", apiEndpoint)
    .waitFor(api);

  // deploy-enrollment-checker.yaml builds and ships a static export for DC as well as
  // CO, so the checker belongs to every state's graph rather than CO's alone.
  //
  // Deployed, the checker reads window.__CHECKER_CONFIG__ from a config.js written into
  // its bucket at deploy time. That file only accompanies a static export, so here,
  // where the checker runs `next dev`, lib/client-config.ts falls back to the build-time
  // env set below. The remaining flags (school field, bot protection, analytics keys)
  // keep their schema defaults; a developer who needs one can set it in the app's
  // .env.local, which Next still reads because these values take precedence over it.
  //
  // NEXT_PUBLIC_API_BASE_URL is deliberately unset: without it the browser calls the
  // checker's own /api/enrollment/* route handlers, which proxy to BACKEND_URL. Those
  // routes are stripped from the static export, where the deployed apiBaseUrl points at
  // the portal instead.
  //
  // STATE and NEXT_PUBLIC_STATE are set together because the checker's next.config.ts
  // derives the latter from the former and overwrites whatever was passed in: STATE
  // alone decides the build, and NEXT_PUBLIC_STATE alone would be discarded, quietly
  // yielding CO's checker.
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

  // The portal returns CORS headers for the checker's origin and the checker links back
  // to the portal, so each needs the other's URL. Endpoint references resolve after
  // allocation, so referencing both directions is fine.
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
