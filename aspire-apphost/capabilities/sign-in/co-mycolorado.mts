// CO sign-in against the MyColorado dev environment, a PingOne tenant. Unlike Keycloak,
// the IdP is remote, so this provider makes no resource and gives only settings.
// `pnpm aspire:co:myco` selects it. Read docs/adr/0008-oidc-mycolorado-authentication-and-state-auth-context.md.

import type { Requirements } from "../requirements.mjs";
import type { SignInCapability, SignInProvider } from "./contract.mjs";

/** The same discovery document that tofu/config/dev-co gives the deployed dev environment. */
const discoveryEndpoint =
  "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as/.well-known/openid-configuration";

/** The origin of the IdP, which the Content Security Policy of the portal must allow. */
const issuerOrigin = "https://auth.pingone.com";

/** PingOne accepts registered redirect URIs only, and localhost:3000/callback is the local one. */
const portalPort = 3000;

const callbackRedirectUri = `http://localhost:${portalPort}/callback`;

function provision(): Promise<SignInCapability> {
  return Promise.resolve({
    requirements: {
      settings: [
        {
          key: "Oidc__DiscoveryEndpoint",
          value: discoveryEndpoint,
          why: "Where the API reads the OIDC metadata of the MyColorado dev tenant.",
        },
        {
          key: "Oidc__StepUp__DiscoveryEndpoint",
          value: discoveryEndpoint,
          why: "The step-up reauthentication uses the same tenant.",
        },
      ],
      // Nothing to wait for. The IdP is not a resource of this graph.
      waits: [],
    },
    bindPortal,
  });
}

function bindPortal(): Promise<Requirements> {
  return Promise.resolve({
    settings: [
      {
        key: "Oidc__CallbackRedirectUri",
        value: callbackRedirectUri,
        why: "The redirect URI that the dev client registers. Step-up falls back to it.",
      },
    ],
    portalSettings: [
      {
        key: "OIDC_ISSUER_ORIGIN",
        value: issuerOrigin,
        why: "Widens the Content Security Policy of the portal to the origin of the IdP.",
      },
    ],
    waits: [],
  });
}

export const coMyColoradoOidc: SignInProvider = {
  name: "OIDC via the MyColorado dev environment",
  signInHint: `Sign in with a MyColorado dev test account. The portal runs at http://localhost:${portalPort}, the port that the dev client registers.`,
  preflight: () => [],
  provision,
};
