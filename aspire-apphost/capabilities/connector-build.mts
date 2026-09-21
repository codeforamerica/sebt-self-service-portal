// The connector build capability: how the plugin of a state gets to the place where the
// API loads it, and where that place is.
//
// The API reads `PluginAssemblyPaths` and loads the DLLs from that directory with MEF.
// Read docs/adr/0007-multi-state-plugin-approach.md. The directory is empty on a new
// checkout, so a build must put the DLLs there before the API starts. Each state builds
// a different project, and both builds use the CopyPlugins target of the plugin csproj.
//
// The 2 states differ in where the connector is. The DC connector is in a second
// repository, and it also needs the NuGet store. The CO connector is in this repository.
// Building the API alone does not build it, because the csproj of the API has no
// reference to it. Before this capability, `plugins-co` held whatever an earlier
// `pnpm api:build-co` put there, and a new checkout started the API with no CO plugin.
//
// The provider that stages the DLLs also gives the path to the API. Thus one module owns
// both halves of the answer. Read ./requirements.mts.

import type { DistributedApplicationBuilder } from "../.aspire/modules/aspire.mjs";
import type { AppHostConfig, SupportedState } from "../config.mjs";
import { coInRepoBuild } from "./connector-build-co.mjs";
import { dcOutOfTreeBuild } from "./connector-build-dc.mjs";
import type { Preflight, Requirements } from "./requirements.mjs";

export interface ConnectorBuildContext {
  builder: DistributedApplicationBuilder;
  config: AppHostConfig;
}

export interface ConnectorBuildCapability {
  requirements: Requirements;
}

export interface ConnectorBuildProvider {
  /** What this state builds. This reads as a row of the capability matrix. */
  readonly name: string;
  /** What a developer does after a change to the connector. */
  readonly buildHint: string;
  /** The obligations of the host machine. The checks run before any resource exists. */
  readonly preflight: (config: AppHostConfig) => readonly Preflight[];
  provision(context: ConnectorBuildContext): Promise<ConnectorBuildCapability>;
}

// The map has a key for each state, and the AppHost does not use a switch. Thus a new
// state in SupportedState is an error of compilation here, until that state can build
// its connector.
const providers: Record<SupportedState, ConnectorBuildProvider> = {
  dc: dcOutOfTreeBuild,
  co: coInRepoBuild,
};

export function connectorBuildProviderFor(
  state: SupportedState,
): ConnectorBuildProvider {
  return providers[state];
}

/**
 * The directory that holds the plugin DLLs of a state, relative to the content root of
 * the API.
 *
 * Both providers use this for the destination of the build and for the value that they
 * give to the API. Thus the 2 values cannot disagree. The API binds the key as an array,
 * and for this reason the name ends with `__0`.
 */
export function pluginDirectory(config: AppHostConfig): string {
  return `plugins-${config.state}`;
}

/** The key that tells the API where to find the plugins. */
export const pluginPathKey = "PluginAssemblyPaths__0";
