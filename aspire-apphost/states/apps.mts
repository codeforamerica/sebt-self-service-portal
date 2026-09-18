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
  // JwtSettings.SecretKey ships empty in appsettings.json, and it is [Required] with a
  // 32-character minimum, so the API fails options validation at startup unless the
  // gitignored appsettings.Development.json supplies it. Generated rather than written
  // here, so no signing key is committed. Tokens issued before a restart stop validating
  // after it, which is harmless locally: they already expire in 15 minutes.
  const jwtSecret = await builder.addParameterWithGeneratedValue(
    "jwt-secret",
    { minLength: 64, lower: true, upper: true, numeric: true },
    { secret: true },
  );

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
    .withEnvironment("JwtSettings__SecretKey", jwtSecret)
    // Where the state's plugin DLLs are staged, relative to the API's content root. The
    // key is absent from appsettings.json and lives only in the gitignored state file, so
    // the API cannot load plugins on a fresh checkout. Derived from the state so a new
    // state module needs no extra wiring; the API binds it as an array, hence `__0`.
    .withEnvironment("PluginAssemblyPaths__0", `plugins-${config.state}`)
    .withOtlpExporter()
    // Log export is opt-in: appsettings.json ships `Otel:UseLogExporter=console`, under
    // which the API registers no OpenTelemetry log provider and Serilog keeps
    // writeToProviders false, leaving the dashboard's structured logs empty. Only the
    // gitignored appsettings.Development.json turns it on, so without this the dashboard
    // depends on a file each developer edits by hand. LoggingSetup reads the value from
    // configuration, which includes this variable, so it also flips writeToProviders.
    //
    // The destination needs no override here: the log exporter takes its address from the
    // standard OTEL_EXPORTER_OTLP_* variables that withOtlpExporter already sets.
    .withEnvironment("Otel__UseLogExporter", "otlp")
    .waitFor(shared.portalDb);

  // withOtlpExporter sets the standard OTEL_EXPORTER_OTLP_ENDPOINT, but
  // OpenTelemetrySetup binds OtlpExporterOptions from the `Otel:OtlpExporter` config
  // section, and appsettings.json sets that to the old Jaeger address. Config wins, so
  // without this override traces and metrics are exported to a port nothing listens on.
  if (config.dashboardOtlpEndpoint) {
    await api.withEnvironment(
      "Otel__OtlpExporter__Endpoint",
      config.dashboardOtlpEndpoint,
    );
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
  // withBrowserLogs tracks a browser Aspire launches itself, so its console output and
  // screenshots reach the dashboard alongside the server-side telemetry. It uses an
  // Aspire-managed Chromium user data directory, never the developer's own profile.
  const web = await builder
    .addNextJsApp("web", resolve(repoRoot, "apps/portal/src/SEBT.Portal.Web"))
    .withPnpm()
    .withEnvironment("STATE", config.state)
    .withEnvironment("BACKEND_URL", apiEndpoint)
    // instrumentation.node.ts calls startOtel, but packages/observability resolves every
    // signal to 'none' unless OTEL_EXPORTER_OTLP_ENDPOINT is set, so the SDK never starts
    // and the app stays dark. withOtlpExporter supplies that address. See
    // docs/adr/0018-web-tier-opentelemetry.md.
    .withOtlpExporter()
    .withBrowserLogs()
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
    // Same reason as the portal: the checker registers startOtel too.
    .withOtlpExporter()
    .withBrowserLogs()
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
