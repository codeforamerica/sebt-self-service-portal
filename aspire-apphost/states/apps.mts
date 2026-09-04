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
  /** CO-only public enrollment checker. */
  checker?: NextJsAppResource;
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
      resolve(repoRoot, "apps/portal/src/SEBT.Portal.Api/SEBT.Portal.Api.csproj"),
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
  const web = await builder
    .addNextJsApp("web", resolve(repoRoot, "apps/portal/src/SEBT.Portal.Web"))
    .withPnpm()
    .withEnvironment("NEXT_PUBLIC_STATE", config.state)
    .withEnvironment("BACKEND_URL", apiEndpoint)
    .waitFor(api);

  if (config.state !== "co") {
    return { web };
  }

  const checker = await builder
    .addNextJsApp(
      "enrollment-checker",
      resolve(repoRoot, "apps/portal/src/SEBT.EnrollmentChecker.Web"),
    )
    .withPnpm()
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
