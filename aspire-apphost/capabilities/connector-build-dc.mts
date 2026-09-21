// The DC connector is in a second repository. Thus 2 one-shot jobs run before the API:
// one registers the local NuGet store, and one builds the plugin and stages its DLLs.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

import { repoRoot } from "../config.mjs";
import type { AppHostConfig } from "../config.mjs";
import {
  pluginDirectory,
  pluginPathKey,
} from "./connector-build.mjs";
import type {
  ConnectorBuildCapability,
  ConnectorBuildContext,
  ConnectorBuildProvider,
} from "./connector-build.mjs";

/** The script of the connector that registers ~/nuget-store. */
const setupScript = "setup.sh";

/** The plugin project, relative to the checkout of the connector. */
const pluginProject =
  "src/SEBT.Portal.StatePlugins.DC/SEBT.Portal.StatePlugins.DC.csproj";

/** The contract project in this repository. */
const contractProject = resolve(
  repoRoot,
  "apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj",
);

function pluginDestination(config: AppHostConfig): string {
  return resolve(
    repoRoot,
    `apps/portal/src/SEBT.Portal.Api/${pluginDirectory(config)}`,
  );
}

async function provision({
  builder,
  config,
}: ConnectorBuildContext): Promise<ConnectorBuildCapability> {
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
      resolve(connectorPath, setupScript),
      connectorPath,
      [],
    )
    .withHiddenOnCompletion();

  // This job waits for the store above. Then a restore that needs the package finds the
  // source.
  //
  // This builds the plugin csproj, and not scripts/dev/build-dc.sh. That script also
  // builds the test project of the connector. That project gets SSH.NET through
  // Testcontainers.MsSql, which fails with NU1903. This occurs if the layout puts the
  // Directory.Build.props of the portal above the connector. The CopyPlugins target of
  // the plugin copies the DLLs in both cases.
  const pluginBuild = await builder
    .addExecutable("dc-plugin-build", "dotnet", connectorPath, [
      "build",
      resolve(connectorPath, pluginProject),
      // If this property is absent, the connector finds the contract from its own
      // location. Then it uses the NuGet package when this repository is not its sibling.
      `-p:StateConnectorInterfacesProject=${contractProject}`,
      `-p:PluginDestDir=${pluginDestination(config)}`,
    ])
    .waitForCompletion(nugetStore)
    .withHiddenOnCompletion();

  return {
    requirements: {
      settings: [
        {
          key: pluginPathKey,
          value: pluginDirectory(config),
          // The key is absent from appsettings.json, and it is in the gitignored state
          // file only. Thus the API cannot load a plugin on a new checkout.
          why: "Where the build above puts the plugin DLLs, relative to the content root of the API.",
        },
      ],
      // The API loads the plugins at start. Thus it must not start before the DLLs are
      // in place.
      waits: [{ resource: pluginBuild, until: "completion" }],
    },
  };
}

export const dcOutOfTreeBuild: ConnectorBuildProvider = {
  name: "a build of the connector repository, outside this one",
  buildHint:
    "After a change to the DC connector, restart the api resource; dc-plugin-build stages the DLLs again.",
  // config.mts makes a check of the checkout itself. These checks are for the 2 paths in
  // the checkout that this capability needs.
  preflight: (config) => [
    {
      description: `DC connector setup script at ${resolve(config.dcConnectorPath, setupScript)}`,
      check: () => {
        const path = resolve(config.dcConnectorPath, setupScript);
        if (!existsSync(path)) {
          throw new Error(
            `DC connector setup script not found at '${path}'. The dc-nuget-store job runs it. Make a check of DC_CONNECTOR_PATH.`,
          );
        }
      },
    },
    {
      description: `DC plugin project at ${resolve(config.dcConnectorPath, pluginProject)}`,
      check: () => {
        const path = resolve(config.dcConnectorPath, pluginProject);
        if (!existsSync(path)) {
          throw new Error(
            `DC plugin project not found at '${path}'. Without it the build stages no DLLs, and the API loads no DC connector.`,
          );
        }
      },
    },
  ],
  provision,
};
