// Resources that are specific to DC. What is left here is the build of the connector.
// The DC connector is outside this repository, so its plugin DLLs must be staged before
// the API loads them.
//
// Two other parts of DC are capabilities, and they are not here:
//   ../capabilities/sign-in-dc.mts          the SMTP sink for the email OTP.
//   ../capabilities/household-source-dc.mts the DcSource database and its seed job.

import { resolve } from "node:path";

import type {
  DistributedApplicationBuilder,
  ExecutableResource,
  ProjectResource,
} from "../.aspire/modules/aspire.mjs";
import { repoRoot } from "../config.mjs";
import type { AppHostConfig } from "../config.mjs";

export interface DcResources {
  /** One-shot job that registers ~/nuget-store. The plugin build can restore from it. */
  nugetStore: ExecutableResource;
  /** One-shot build that puts the DC plugin DLLs into plugins-dc. */
  pluginBuild: ExecutableResource;
}

export async function addDcResources(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
  api: ProjectResource,
): Promise<DcResources> {
  const connectorPath = config.dcConnectorPath;

  // The csproj of the connector uses a ProjectReference for the plugin contract when it
  // can see this repository as a sibling. If it cannot see it, the csproj uses the
  // SEBT.Portal.StatesPlugins.Interfaces package. This graph can get that second path.
  // DC_CONNECTOR_PATH can point to a checkout that is not a sibling. The connector finds
  // the contract path from its own location, and not from ours. Then the package must
  // come from ~/nuget-store.
  //
  // This runs the setup.sh of the connector, and it does not write that script again.
  // The script is idempotent. It makes the directory with `mkdir -p`, and it changes the
  // NuGet source if a source is present. The script writes to the global NuGet
  // configuration of the user. This is the one part of this graph that changes data
  // outside the two repositories.
  const nugetStore = await builder
    .addExecutable(
      "dc-nuget-store",
      resolve(connectorPath, "setup.sh"),
      connectorPath,
      [],
    )
    .withHiddenOnCompletion();

  // The DC connector builds outside this repository. Thus its plugin DLLs must go into
  // plugins-dc before the API loads the plugins at start. This job waits for the store
  // above. Then a restore that needs the package finds the source.
  //
  // This builds the plugin csproj, and not scripts/dev/build-dc.sh. That script also
  // builds the test project of the connector. That project gets SSH.NET through
  // Testcontainers.MsSql, which fails with NU1903. This occurs if the layout puts the
  // Directory.Build.props of the portal above the connector. The CopyPlugins target of
  // the plugin copies the DLLs in both cases.
  const pluginBuild = await builder
    .addExecutable("dc-plugin-build", "dotnet", connectorPath, [
      "build",
      resolve(
        connectorPath,
        "src/SEBT.Portal.StatePlugins.DC/SEBT.Portal.StatePlugins.DC.csproj",
      ),
      // If this property is absent, the connector finds the contract from its own
      // location. Then it uses the NuGet package when this repository is not its sibling.
      `-p:StateConnectorInterfacesProject=${resolve(
        repoRoot,
        "apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj",
      )}`,
      `-p:PluginDestDir=${resolve(
        repoRoot,
        `apps/portal/src/SEBT.Portal.Api/plugins-${config.state}`,
      )}`,
    ])
    .waitForCompletion(nugetStore)
    .withHiddenOnCompletion();

  await api.waitForCompletion(pluginBuild);

  return { nugetStore, pluginBuild };
}
