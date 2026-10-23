// The CO connector is in this repository, but the csproj of the API has no reference to
// it. Thus a build of the API alone stages no CO plugin. One one-shot job builds the
// plugin project, and its CopyPlugins target puts the DLLs in plugins-co.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

import { repoRoot } from "../../config.mjs";
import type { AppHostConfig } from "../../config.mjs";
import {
  pluginDirectory,
  pluginPathKey,
} from "./contract.mjs";
import type {
  ConnectorBuildCapability,
  ConnectorBuildContext,
  ConnectorBuildProvider,
} from "./contract.mjs";

/** The plugin project of CO, in this repository. */
const pluginProject = resolve(
  repoRoot,
  "apps/connectors/co/src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj",
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
  // This builds the plugin csproj, and not scripts/dev/build-co.sh. That script runs
  // `dotnet build` at the root of the repository, which also builds each test project.
  // The CopyPlugins target of the plugin stages the DLLs in both cases.
  //
  // The contract needs no property here. The csproj of CO is in this repository, so it
  // finds the contract with a ProjectReference. DC needs the property, because its
  // connector is outside this repository.
  const pluginBuild = await builder
    .addExecutable("co-plugin-build", "dotnet", repoRoot, [
      "build",
      pluginProject,
      `-p:PluginDestDir=${pluginDestination(config)}`,
    ])
    // A finished one-shot job otherwise stays in the dashboard and looks like a fault.
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

export const coInRepoBuild: ConnectorBuildProvider = {
  name: "a build of the CO plugin project in this repository",
  buildHint:
    "After a change to the CO connector, restart the api resource; co-plugin-build stages the DLLs again.",
  // The project is in this repository, so a check of the path finds a move or a rename,
  // and not an absent checkout.
  preflight: () => [
    {
      description: `CO plugin project at ${pluginProject}`,
      check: () => {
        if (!existsSync(pluginProject)) {
          throw new Error(
            `CO plugin project not found at '${pluginProject}'. Without it the build stages no DLLs, and the API loads no CO connector.`,
          );
        }
      },
    },
  ],
  provision,
};
