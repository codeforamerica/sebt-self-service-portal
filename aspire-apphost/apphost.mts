// Aspire AppHost for the SEBT Self-Service Portal.
//
// This AppHost composes the resource graph for one state. The graph has the shared
// resources and the resources of that one state. DC and CO need different dependencies.
// DC needs a DcSource database. CO needs Redis and an OIDC provider. Thus the AppHost
// builds one graph for one state. It does not model each state and start a subset.
//
// Two kinds of module give the resources:
//
//   states/       the resources of a state, wired directly onto the API.
//   capabilities/ the infrastructure that a state needs to satisfy one requirement of
//                 the application. A capability is a provider. The provider returns what
//                 it needs, and it does not change the API. Sign-in is the first
//                 capability. Read capabilities/requirements.mts.
//
// Each value, and also the state, comes from ./config.mts.
// To add a state, add a states/<state>.mts module, a sign-in provider, and one arm to
// the switch below.
//
// Usage: pnpm aspire:dc or pnpm aspire:co

import { createBuilder } from "./.aspire/modules/aspire.mjs";
import { householdSourceProviderFor } from "./capabilities/household-source.mjs";
import {
  applyRequirements,
  runPreflight,
} from "./capabilities/requirements.mjs";
import { signInProviderFor } from "./capabilities/sign-in.mjs";
import { loadConfig } from "./config.mjs";
import { addApi, addWebApps } from "./states/apps.mjs";
import { addCoResources } from "./states/co.mjs";
import { addDcResources } from "./states/dc.mjs";
import { addSharedResources } from "./states/shared.mjs";

const config = loadConfig();
console.log(`[apphost] composing resource graph for STATE=${config.state}`);

const signIn = signInProviderFor(config.state);
const householdSource = householdSourceProviderFor(config.state);
console.log(`[sign-in] ${signIn.name}. ${signIn.signInHint}`);
console.log(`[household-source] ${householdSource.name}. ${householdSource.dataHint}`);

// The checks run before the builder exists. Thus an unsatisfied obligation costs one
// second, and it does not give a graph that is half started.
await runPreflight("sign-in", signIn.preflight(config));
await runPreflight("household-source", householdSource.preflight(config));

const builder = await createBuilder();

const shared = await addSharedResources(builder, config);
const api = await addApi(builder, config, shared);

// A state module attaches its own API environment and its own waits to the resource
// above. Each capability that a state module loses makes this arm smaller.
switch (config.state) {
  case "dc":
    await addDcResources(builder, config, api);
    break;
  case "co":
    await addCoResources(builder, config, api);
    break;
}

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
