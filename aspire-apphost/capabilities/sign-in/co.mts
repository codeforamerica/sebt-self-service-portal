// CO gives identity to an OIDC provider. On a local machine that provider is Keycloak.
// Keycloak is a stand-in for MyColorado. Thus a developer can do a login, a logout, and
// a step-up with no third-party IdP. This replaces the `keycloak` profile of compose.
// Read docs/adr/0019-keycloak-local-oidc-stand-in.md and
// docs/development/keycloak-oidc.md.

import { existsSync } from "node:fs";
import { resolve } from "node:path";

import { EndpointProperty, refExpr } from "../../.aspire/modules/aspire.mjs";
import type { NextJsAppResource } from "../../.aspire/modules/aspire.mjs";
import { repoRoot } from "../../config.mjs";
import { trustedDeveloperCertificate } from "../developer-certificate.mjs";
import type { Requirements } from "../requirements.mjs";
import type {
  SignInCapability,
  SignInContext,
  SignInProvider,
} from "./contract.mjs";

/**
 * The host port of Keycloak. This port is fixed, and Aspire does not allocate it.
 *
 * Keycloak makes the issuer that it puts in a token from the address that a client used.
 * Thus the browser and the API must use one URL. compose,
 * appsettings.keycloak.example.json, and docs/development/keycloak-oidc.md already use
 * 8180. Thus the admin console stays at the address that the guide gives.
 */
const keycloakPort = 8180;

/**
 * Keycloak has one endpoint, and its name is `http`. Aspire gives the container a
 * developer certificate and then serves that endpoint with TLS. This is the same
 * behavior that states/co.mts documents for Redis: the endpoint keeps its name, and the
 * scheme changes. There is no endpoint with the name `https`.
 */
const keycloakEndpointName = "http";

/** The realm that Keycloak imports from docker/keycloak/sebt-realm.json. */
const realm = "sebt";

/** The OIDC metadata of the realm. The API reads it, and the health check below reads it. */
const discoveryPath = `/realms/${realm}/.well-known/openid-configuration`;

/** The realm definition that Keycloak imports at start. */
const realmFile = resolve(repoRoot, "docker/keycloak/sebt-realm.json");

/** The custom `sebt` login theme that the realm selects. */
const themesDirectory = resolve(repoRoot, "docker/keycloak/themes");

/**
 * The placeholder that the clients of the realm use for the origin of the portal.
 *
 * sebt-realm.json writes `${SEBT_PORTAL_ORIGIN}` into the redirect URI, the web origin,
 * and the post-logout URI of each client. Keycloak reads the value from the environment
 * during the import. Thus the portal can keep a port that Aspire allocates, because the
 * realm gets the real origin at the import. It does not register a fixed origin. compose
 * sets the same variable to localhost:3000, where compose always serves the portal.
 */
const portalOriginVariable = "SEBT_PORTAL_ORIGIN";

async function provision({ builder }: SignInContext): Promise<SignInCapability> {
  // These credentials are for a local machine only, and that is intentional. The realm
  // has fixture users with one password. The guide documents these admin credentials.
  // This module gives the password, and it does not let Aspire make one. Thus the admin
  // console accepts the value that the guide gives.
  const keycloakAdmin = await builder.addParameter("keycloak-admin", {
    value: "admin",
  });
  const keycloakAdminPassword = await builder.addParameter(
    "keycloak-admin-password",
    { value: "admin", secret: true },
  );

  // There is no data volume. Thus the realm stays in the ephemeral store of the
  // container, as it did with compose. This keeps the reset loop that the guide
  // documents: change sebt-realm.json, make the container again, and the import runs
  // again.
  const keycloak = await builder
    .addKeycloak("keycloak", {
      port: keycloakPort,
      adminUsername: keycloakAdmin,
      adminPassword: keycloakAdminPassword,
    })
    .withRealmImport(realmFile)
    // The realm import covers the import directory only. Thus the theme needs its own
    // mount.
    .withBindMount(themesDirectory, "/opt/keycloak/themes", {
      isReadOnly: true,
    })
    // Aspire already gives Keycloak a health check on its management port. That check
    // reports healthy while the realm is absent, so it cannot show the one fault that
    // stops a login. This check reads the document that the API reads. Thus `healthy`
    // means that the realm exists and that the address in Oidc__DiscoveryEndpoint
    // answers.
    .withHttpHealthCheck({
      path: discoveryPath,
      statusCode: 200,
      endpointName: keycloakEndpointName,
    });

  // The scheme is explicit, and the endpoint reference does not give it.
  //
  // Aspire publishes container port 8443 only, and it binds the fixed host port 8180 to
  // it. Keycloak also listens on 8080 in the container, but nothing publishes that port.
  // An `http://localhost:8180` address therefore accepts a TCP connection and then
  // answers nothing. The API cannot read the discovery document,
  // OidcController.Authorize catches the error, and it sends the person back to /login
  // with the log line `reason=discovery_failed`. The browser never sees Keycloak.
  //
  // The host stays localhost and the port comes from the allocated endpoint. This is the
  // same shape that bindPortal below uses. A trusted developer certificate is necessary.
  // Run `aspire certs trust` one time on each machine.
  // Each expression comes from the port property. `refExpr` cannot hold another
  // reference expression: the nested value marshals as a plain object, and
  // `withEnvironment` rejects it with TYPE_MISMATCH.
  const keycloakEndpoint = await keycloak.getEndpoint(keycloakEndpointName);
  const keycloakPortValue = await keycloakEndpoint.property(EndpointProperty.Port);
  const keycloakOrigin = refExpr`https://localhost:${keycloakPortValue}`;
  const discoveryEndpoint = refExpr`https://localhost:${keycloakPortValue}${discoveryPath}`;

  /**
   * Closes the loop between the portal and Keycloak, after the portal has an endpoint.
   *
   * Both sides need the same origin, and neither side can hold a fixed value. Keycloak
   * puts the origin into the redirect URIs of the realm at the import. The API sends the
   * origin as the callback that it expects.
   */
  async function bindPortal(web: NextJsAppResource): Promise<Requirements> {
    const portalEndpoint = await web.getEndpoint("http");

    // Keycloak is a container. Thus an endpoint reference that goes to Keycloak resolves
    // to the view of the portal from the container network, which is
    // `http://aspire.dev.internal:<port>`. The realm would then register a redirect URI
    // that the browser never sends, and Keycloak answers "Invalid parameter:
    // redirect_uri". The browser reads this value, and Keycloak does not. Thus the host
    // stays localhost, and the port still comes from the endpoint that Aspire allocated.
    await keycloak.withEnvironment(
      portalOriginVariable,
      refExpr`http://localhost:${await portalEndpoint.property(EndpointProperty.Port)}`,
    );

    return {
      settings: [
        {
          key: "Oidc__CallbackRedirectUri",
          value: refExpr`${portalEndpoint}/callback`,
          why: "Where the API sends the browser after the IdP round trip, on a port that Aspire allocates.",
        },
      ],
      // src/proxy.ts reads OIDC_ISSUER_ORIGIN and puts it in the connect-src and the
      // form-action of the Content Security Policy. Only scripts/preview/deploy-co.sh
      // sets it today. Without it, form-action stays `'self'`, because the local
      // widening of the policy covers connect-src and not form-action.
      portalSettings: [
        {
          key: "OIDC_ISSUER_ORIGIN",
          value: keycloakOrigin,
          why: "Widens the Content Security Policy of the portal to the origin of the IdP.",
        },
      ],
      waits: [],
    };
  }

  return {
    // These settings give what docs/development/keycloak-oidc.md asks a developer to
    // merge by hand from appsettings.keycloak.example.json. The repository holds that
    // example only, and it has no appsettings.co.json. Without these settings, the CO
    // graph starts with no OIDC client.
    //
    // The client ids and the client secrets are fixtures of the realm. That example file
    // already publishes them, and they have no value outside this container.
    // Oidc:CompleteLoginSigningKey stays as it is, because the placeholder in
    // appsettings.json is longer than its minimum of 32 characters.
    requirements: {
      settings: [
        {
          key: "Oidc__DiscoveryEndpoint",
          value: discoveryEndpoint,
          why: "Where the API reads the OIDC metadata of the realm.",
        },
        {
          key: "Oidc__ClientId",
          value: "sebt-portal",
          why: "The fixture client of the realm for the login flow of the portal.",
        },
        {
          key: "Oidc__ClientSecret",
          value: "sebt-portal-dev-secret",
          why: "The fixture secret of that client. appsettings.keycloak.example.json publishes it.",
        },
        {
          key: "Oidc__StepUp__DiscoveryEndpoint",
          value: discoveryEndpoint,
          why: "The step-up reauthentication uses the same realm.",
        },
        {
          key: "Oidc__StepUp__ClientId",
          value: "sebt-portal-stepup",
          why: "The separate client of the realm for step-up, which asks for a higher assurance level.",
        },
        {
          key: "Oidc__StepUp__ClientSecret",
          value: "sebt-portal-stepup-dev-secret",
          why: "The fixture secret of that client.",
        },
        // The fixture users of the realm get a household only when the seed makes the
        // addresses that they sign in with. Thus a person cannot sign in against Keycloak
        // and read the real CBMS data at the same time. This graph selects Keycloak, and
        // for that reason states/co.mts turns the mock household data on.
        //
        // The pattern connects the 2 halves. SeedingSettings.BuildEmail puts the name of
        // the scenario into the pattern. The 3 users of the realm have the addresses that
        // the pattern makes. The scenarios are co-loaded, verified, and non-co-loaded.
        {
          key: "Seeding__EmailPattern",
          value: "sebt.co+{0}@codeforamerica.org",
          why: "Makes the seeded households have the addresses that the 3 users of the realm sign in with.",
        },
        // Step 3 of the guide. A phone override from a different flow loads a household
        // that does not belong to the user who signed in. This looks like a fault in the
        // data, and not a fault in the configuration.
        {
          key: "DevelopmentPhoneOverride__Phone",
          value: "",
          why: "An override that stays would load a household that the user who signed in does not own.",
          kind: "neutralize",
        },
        // This is the same risk as the phone override. This value changes the address of
        // the co-loaded seed user. Thus a value in the appsettings.Development.json of a
        // developer would break one of the 3 Keycloak logins, and the other 2 would
        // continue to work.
        {
          key: "Seeding__CoLoadedSeedEmailOverride",
          value: "",
          why: "An override that stays breaks one of the 3 fixture logins, which is difficult to read as a fault in the configuration.",
          kind: "neutralize",
        },
      ],
      waits: [{ resource: keycloak, until: "healthy" }],
    },
    bindPortal,
  };
}

export const coKeycloakOidc: SignInProvider = {
  name: "OIDC via Keycloak",
  signInHint: `Sign in as one of the realm's three fixture users (co-loaded, verified, non-co-loaded); the admin console is at https://localhost:${keycloakPort} with admin/admin.`,
  // The container mounts both paths. If a path is absent, the fault is not visible until
  // a login fails, because Keycloak starts in both cases. Both paths come from the
  // repository root, so this function does not read the configuration.
  //
  // Keycloak serves TLS with the developer certificate, and the API reads the discovery
  // document from that address. Thus this capability needs the certificate too.
  preflight: () => [
    trustedDeveloperCertificate,
    {
      description: `Keycloak realm import file at ${realmFile}`,
      check: () => {
        if (!existsSync(realmFile)) {
          throw new Error(
            `Keycloak realm file not found at '${realmFile}'. Without it the '${realm}' realm is never imported, and the portal has no OIDC client to sign in against.`,
          );
        }
      },
    },
    {
      description: `Keycloak login theme at ${themesDirectory}`,
      check: () => {
        if (!existsSync(themesDirectory)) {
          throw new Error(
            `Keycloak theme directory not found at '${themesDirectory}'. The realm selects the '${realm}' login theme from this mount.`,
          );
        }
      },
    },
  ],
  provision,
};
