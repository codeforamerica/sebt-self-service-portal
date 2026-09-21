// Aspire AppHost for the SEBT Self-Service Portal.
//
// This AppHost composes the resource graph for one state. The graph has the shared
// resources and the resources of that one state. DC and CO need different dependencies.
// DC needs a DcSource database. CO needs Redis and an OIDC provider. Thus the AppHost
// builds one graph for one state. It does not model each state and start a subset.
//
// Two kinds of module give the resources:
//
//   states/       the resources that each state shares. These are the API, the 2 web
//                 applications, and the database of the portal. No state owns them.
//   capabilities/ the infrastructure that a state needs to satisfy one requirement of
//                 the application. A capability is a provider. The provider returns what
//                 it needs, and it does not change the API. There are 4 capabilities:
//                 the cache, the connector build, the household source, and sign-in.
//                 Read capabilities/requirements.mts.
//
// Each value, and also the state, comes from ./config.mts.
// To add a state, add one provider for each capability. The type of each registry is
// `Record<SupportedState, ...>`, so the compiler gives the list of what is absent.
//
// Usage: pnpm aspire:dc or pnpm aspire:co

import { createBuilder } from "./.aspire/modules/aspire.mjs";
import { cacheProviderFor } from "./capabilities/cache.mjs";
import { connectorBuildProviderFor } from "./capabilities/connector-build.mjs";
import { householdSourceProviderFor } from "./capabilities/household-source.mjs";
import {
  applyRequirements,
  runPreflight,
} from "./capabilities/requirements.mjs";
import { signInProviderFor } from "./capabilities/sign-in.mjs";
import { loadConfig } from "./config.mjs";
import { addApi, addWebApps } from "./states/apps.mjs";
import { addSharedResources } from "./states/shared.mjs";

const config = loadConfig();
console.log(`[apphost] composing resource graph for STATE=${config.state}`);

const cache = cacheProviderFor(config.state);
const connectorBuild = connectorBuildProviderFor(config.state);
const householdSource = householdSourceProviderFor(config.state);
const signIn = signInProviderFor(config.state);
console.log(`[cache] ${cache.name}. ${cache.cacheHint}`);
console.log(`[connector-build] ${connectorBuild.name}. ${connectorBuild.buildHint}`);
console.log(`[household-source] ${householdSource.name}. ${householdSource.dataHint}`);
console.log(`[sign-in] ${signIn.name}. ${signIn.signInHint}`);

// The checks run before the builder exists. Thus an unsatisfied obligation costs one
// second, and it does not give a graph that is half started.
await runPreflight("cache", cache.preflight(config));
await runPreflight("connector-build", connectorBuild.preflight(config));
await runPreflight("household-source", householdSource.preflight(config));
await runPreflight("sign-in", signIn.preflight(config));

const builder = await createBuilder();

const shared = await addSharedResources(builder, config);
const api = await addApi(builder, config, shared);

// Each part of a state is a capability now. Neither state has a module of its own, so
// there is no switch here.
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
  saPassword: shared.saPassword,
});
await applyRequirements(
  { api },
  "household-source",
  householdSourceCapability.requirements,
);

const signInCapability = await signIn.provision({ builder, config });
await applyRequirements({ api }, "sign-in", signInCapability.requirements);

const apps = await addWebApps(builder, config, api);

// This is last, because it is the one part of the wiring that reads the endpoint of the
// portal. The realm of CO redirects back to that endpoint. Thus neither side can hold a
// fixed port.
if (signInCapability.bindPortal) {
  await applyRequirements(
    { api, portal: apps.web },
    "sign-in portal binding",
    await signInCapability.bindPortal(apps.web),
  );
}

await builder.build().run();
