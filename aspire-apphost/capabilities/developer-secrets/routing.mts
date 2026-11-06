// Which Doppler secret goes to which application, and under which key.
//
// The list is explicit, so a secret reaches an application only when a line here says why.
// A Doppler key that is not listed is reported in the startup log instead of dropped quietly.

/** The application that receives a secret. */
export type SecretTarget = "api" | "web";

export interface ForwardedSecret {
  /** The key as Doppler stores it. */
  dopplerKey: string;
  /** The key in the casing the application reads, so it cannot collide with a key set elsewhere. */
  appKey: string;
  target: SecretTarget;
  /** Why the application needs the value. Write one line. */
  why: string;
}

/** The API key that shared/apps.mts otherwise fills with a generated value. */
export const jwtSecretKey = "JwtSettings__SecretKey";

export const forwardedSecrets: readonly ForwardedSecret[] = [
  {
    dopplerKey: "JWTSETTINGS__SECRETKEY",
    appKey: jwtSecretKey,
    target: "api",
    why: "Signs the session tokens of the API.",
  },
  {
    dopplerKey: "IDENTIFIERHASHER__SECRETKEY",
    appKey: "IdentifierHasher__SecretKey",
    target: "api",
    why: "Hashes identifiers before the portal database stores them.",
  },
  {
    dopplerKey: "OIDC__COMPLETELOGINSIGNINGKEY",
    appKey: "Oidc__CompleteLoginSigningKey",
    target: "api",
    why: "Signs the handoff that completes an OIDC sign-in.",
  },
  {
    dopplerKey: "SMARTY__AUTHID",
    appKey: "Smarty__AuthId",
    target: "api",
    why: "Authenticates the address validation of the API with Smarty.",
  },
  {
    dopplerKey: "SMARTY__AUTHTOKEN",
    appKey: "Smarty__AuthToken",
    target: "api",
    why: "Authenticates the address validation of the API with Smarty.",
  },
  {
    dopplerKey: "CBMS__CLIENTID",
    appKey: "Cbms__ClientId",
    target: "api",
    why: "Authenticates the CO connector with the CBMS sandbox when its mock is off.",
  },
  {
    dopplerKey: "CBMS__CLIENTSECRET",
    appKey: "Cbms__ClientSecret",
    target: "api",
    why: "Authenticates the CO connector with the CBMS sandbox when its mock is off.",
  },
  {
    dopplerKey: "SOCURE__APIKEY",
    appKey: "Socure__ApiKey",
    target: "api",
    why: "Authenticates ID proofing with the Socure sandbox when the stub is off.",
  },
  {
    dopplerKey: "SOCURE__WEBHOOKSECRET",
    appKey: "Socure__WebhookSecret",
    target: "api",
    why: "Checks the bearer token on Socure webhooks.",
  },
  {
    dopplerKey: "SMARTY_EMBEDDED_KEY",
    appKey: "SMARTY_EMBEDDED_KEY",
    target: "web",
    why: "Turns on Smarty address autocomplete in the browser.",
  },
  {
    dopplerKey: "SOCURE_DI_SDK_KEY",
    appKey: "SOCURE_DI_SDK_KEY",
    target: "web",
    why: "Loads the Socure device intelligence SDK in the browser.",
  },
];

export interface RoutedSecret {
  appKey: string;
  /** The name of the Aspire secret parameter that holds the value. */
  parameterName: string;
  value: string;
  why: string;
}

export interface RoutedSecrets {
  api: RoutedSecret[];
  web: RoutedSecret[];
  /** Doppler keys with no line in forwardedSecrets, sorted for the log. */
  notForwarded: string[];
}

function parameterNameFor(appKey: string): string {
  return `doppler-${appKey.toLowerCase().replace(/_+/g, "-")}`;
}

/** Sorts the Doppler values into the secrets each application gets. An empty value is skipped. */
export function routeSecrets(values: ReadonlyMap<string, string>): RoutedSecrets {
  const routed: RoutedSecrets = { api: [], web: [], notForwarded: [] };

  for (const secret of forwardedSecrets) {
    const value = values.get(secret.dopplerKey);
    if (!value) {
      continue;
    }
    routed[secret.target].push({
      appKey: secret.appKey,
      parameterName: parameterNameFor(secret.appKey),
      value,
      why: secret.why,
    });
  }

  const listed = new Set(forwardedSecrets.map((secret) => secret.dopplerKey));
  routed.notForwarded = [...values.keys()].filter((key) => !listed.has(key)).sort();
  return routed;
}

/** Stops the AppHost when a Doppler secret would override a key that a capability sets on purpose. */
export function assertNoCapabilityOverlap(
  routed: RoutedSecrets,
  capabilityKeys: readonly string[],
): void {
  const capabilityKeySet = new Set(capabilityKeys.map((key) => key.toLowerCase()));
  const overlapping = [...routed.api, ...routed.web]
    .map((secret) => secret.appKey)
    .filter((appKey) => capabilityKeySet.has(appKey.toLowerCase()));

  if (overlapping.length > 0) {
    throw new Error(
      `Doppler secrets would override keys that a capability sets: ${overlapping.join(", ")}. ` +
        "Remove them from capabilities/developer-secrets/routing.mts, or from the capability.",
    );
  }
}
