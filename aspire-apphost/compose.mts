// How the resource graph is put together.
//
// This is separate from ./apphost.mts, which is the entry point and holds no wiring.
// Thus one file answers the question of what the graph contains, and a second file
// answers the question of how it starts. A later test can also call `composeGraph` with
// a builder of its own.

import type { DistributedApplicationBuilder } from "./.aspire/modules/aspire.mjs";
import { cacheProviderFor } from "./capabilities/cache/contract.mjs";
import type { CacheProvider } from "./capabilities/cache/contract.mjs";
import { connectorBuildProviderFor } from "./capabilities/connector-build/contract.mjs";
import type { ConnectorBuildProvider } from "./capabilities/connector-build/contract.mjs";
import { householdSourceProviderFor } from "./capabilities/household-source/contract.mjs";
import type { HouseholdSourceProvider } from "./capabilities/household-source/contract.mjs";
import {
  applyRequirements,
  runPreflight,
} from "./capabilities/requirements.mjs";
import { signInProviderFor } from "./capabilities/sign-in/contract.mjs";
import type { SignInProvider } from "./capabilities/sign-in/contract.mjs";
import type { AppHostConfig, SupportedState } from "./config.mjs";
import { addApi, addWebApps } from "./shared/apps.mjs";
import { addPortalDatabase } from "./shared/database.mjs";

/** The provider that answers each capability, for one state. */
export interface StateCapabilities {
  cache: CacheProvider;
  connectorBuild: ConnectorBuildProvider;
  householdSource: HouseholdSourceProvider;
  signIn: SignInProvider;
}

export function capabilitiesFor(state: SupportedState): StateCapabilities {
  return {
    cache: cacheProviderFor(state),
    connectorBuild: connectorBuildProviderFor(state),
    householdSource: householdSourceProviderFor(state),
    signIn: signInProviderFor(state),
  };
}

/** Prints how this state answers each capability, and what a developer does with it. */
export function announceCapabilities(config: AppHostConfig): void {
  const { cache, connectorBuild, householdSource, signIn } = capabilitiesFor(
    config.state,
  );
  console.log(`[cache] ${cache.name}. ${cache.cacheHint}`);
  console.log(`[connector-build] ${connectorBuild.name}. ${connectorBuild.buildHint}`);
  console.log(`[household-source] ${householdSource.name}. ${householdSource.dataHint}`);
  console.log(`[sign-in] ${signIn.name}. ${signIn.signInHint}`);
}

/**
 * Runs the host obligations of each capability.
 *
 * This runs before the builder exists. Thus an unsatisfied obligation costs one second,
 * and it does not give a graph that is half started.
 */
export async function runCapabilityPreflight(
  config: AppHostConfig,
): Promise<void> {
  const { cache, connectorBuild, householdSource, signIn } = capabilitiesFor(
    config.state,
  );
  await runPreflight("cache", cache.preflight(config));
  await runPreflight("connector-build", connectorBuild.preflight(config));
  await runPreflight("household-source", householdSource.preflight(config));
  await runPreflight("sign-in", signIn.preflight(config));
}

/**
 * Makes the resources of one state and discharges what each capability needs.
 *
 * Each part of a state is a capability, so there is no switch on the state here.
 */
export async function composeGraph(
  builder: DistributedApplicationBuilder,
  config: AppHostConfig,
): Promise<void> {
  const { cache, connectorBuild, householdSource, signIn } = capabilitiesFor(
    config.state,
  );

  const database = await addPortalDatabase(builder, config);
  const api = await addApi(builder, config, database);

  const cacheCapability = await cache.provision({ builder, config });
  await applyRequirements({ api }, "cache", cacheCapability.requirements);

  const connectorBuildCapability = await connectorBuild.provision({
    builder,
    config,
  });
  await applyRequirements(
    { api },
    "connector-build",
    connectorBuildCapability.requirements,
  );

  const householdSourceCapability = await householdSource.provision({
    builder,
    config,
    saPassword: database.saPassword,
  });
  await applyRequirements(
    { api },
    "household-source",
    householdSourceCapability.requirements,
  );

  const signInCapability = await signIn.provision({ builder, config });
  await applyRequirements({ api }, "sign-in", signInCapability.requirements);

  const apps = await addWebApps(builder, config, api);

  // This is last, because it is the one part of the wiring that reads the endpoint of
  // the portal. The realm of CO redirects back to that endpoint. Thus neither side can
  // hold a fixed port.
  if (signInCapability.bindPortal) {
    await applyRequirements(
      { api, portal: apps.web },
      "sign-in portal binding",
      await signInCapability.bindPortal(apps.web),
    );
  }
}
