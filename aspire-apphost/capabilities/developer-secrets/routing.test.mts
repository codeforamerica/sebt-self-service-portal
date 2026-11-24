import { strict as assert } from "node:assert";
import { describe, it } from "node:test";

import type { AppHostConfig, CoSignIn, SupportedState } from "../../config.mjs";
import type { DeveloperSecrets } from "./doppler.mjs";
import {
  assertNoCapabilityOverlap,
  assertSignInSecrets,
  forwardedSecrets,
  routeSecrets,
} from "./routing.mjs";

const allDopplerKeys = forwardedSecrets.map((secret) => secret.dopplerKey);

const myColoradoDopplerKeys = [
  "OIDC__CLIENTID",
  "OIDC__CLIENTSECRET",
  "OIDC__STEPUP__CLIENTID",
  "OIDC__STEPUP__CLIENTSECRET",
];

const myColoradoAppKeys = [
  "Oidc__ClientId",
  "Oidc__ClientSecret",
  "Oidc__StepUp__ClientId",
  "Oidc__StepUp__ClientSecret",
];

function valuesFor(keys: readonly string[]): Map<string, string> {
  return new Map(keys.map((key) => [key, `value-of-${key}`]));
}

function configFor(state: SupportedState, coSignIn: CoSignIn = "keycloak"): AppHostConfig {
  return {
    state,
    coSignIn,
    sqlPassword: "",
    redisPassword: "",
    dcConnectorPath: "",
    dashboardOtlpEndpoint: undefined,
    useDoppler: true,
  };
}

const coKeycloak = configFor("co");
const coMyColorado = configFor("co", "mycolorado");

const fromDoppler: DeveloperSecrets = {
  source: "doppler",
  project: "the-project",
  config: "local_personal",
  values: new Map(),
};
const dopplerNotSetUp: DeveloperSecrets = {
  source: "local-files",
  reason: "not-set-up",
  expectedProject: "the-project",
};
const dopplerDisabled: DeveloperSecrets = { source: "local-files", reason: "disabled" };
const noneRouted = { api: [], web: [], notForwarded: [] };

describe("forwardedSecrets", () => {
  it("sends the browser keys to the portal, not the API", () => {
    for (const browserKey of ["SMARTY_EMBEDDED_KEY", "SOCURE_DI_SDK_KEY"]) {
      const secret = forwardedSecrets.find((entry) => entry.dopplerKey === browserKey);
      assert.equal(secret?.target, "web", `${browserKey} must go to the portal`);
      assert.equal(secret?.appKey, browserKey);
    }
  });

  it("names each Doppler key and each app key once, and says why it is forwarded", () => {
    const appKeys = forwardedSecrets.map((secret) => secret.appKey.toLowerCase());
    assert.equal(new Set(allDopplerKeys).size, allDopplerKeys.length);
    assert.equal(new Set(appKeys).size, appKeys.length);
    for (const secret of forwardedSecrets) {
      assert.ok(secret.why.trim().length > 0, `${secret.dopplerKey} has no reason`);
    }
  });
});

describe("routeSecrets", () => {
  it("sends each listed key to its app and reports the keys it does not forward", () => {
    // MyColorado sign-in is the one mode in which each listed key applies.
    const routed = routeSecrets(
      valuesFor([...allDopplerKeys, "EXTRA_KEY", "ANOTHER"]),
      coMyColorado,
    );
    for (const target of ["api", "web"] as const) {
      assert.deepEqual(
        routed[target].map((secret) => secret.appKey),
        forwardedSecrets.filter((secret) => secret.target === target).map((secret) => secret.appKey),
      );
    }
    assert.deepEqual(routed.notForwarded, ["ANOTHER", "EXTRA_KEY"]);
  });

  it("forwards a key under the casing the app uses", () => {
    const routed = routeSecrets(valuesFor(["JWTSETTINGS__SECRETKEY"]), coKeycloak);
    assert.deepEqual(
      routed.api.map((secret) => [secret.appKey, secret.value]),
      [["JwtSettings__SecretKey", "value-of-JWTSETTINGS__SECRETKEY"]],
    );
  });

  it("gives each secret a valid Aspire parameter name", () => {
    const routed = routeSecrets(valuesFor(allDopplerKeys), coMyColorado);
    const names = new Map(
      [...routed.api, ...routed.web].map((secret) => [secret.appKey, secret.parameterName]),
    );
    assert.equal(names.get("JwtSettings__SecretKey"), "doppler-jwtsettings-secretkey");
    assert.equal(names.get("SMARTY_EMBEDDED_KEY"), "doppler-smarty-embedded-key");
    for (const name of names.values()) {
      assert.match(name, /^[a-z][a-z0-9]*(-[a-z0-9]+)*$/);
    }
  });

  it("skips a listed key that Doppler does not have", () => {
    const routed = routeSecrets(valuesFor(["SMARTY__AUTHID"]), coKeycloak);
    assert.deepEqual(routed.api.map((secret) => secret.appKey), ["Smarty__AuthId"]);
    assert.deepEqual(routed.web, []);
    assert.deepEqual(routed.notForwarded, []);
  });

  it("skips a listed key whose value is empty, so it cannot blank out a local value", () => {
    const routed = routeSecrets(new Map([["SMARTY__AUTHID", ""]]), coKeycloak);
    assert.deepEqual(routed.api, []);
  });

  it("forwards the MyColorado client keys to the API when CO signs in with MyColorado", () => {
    const routed = routeSecrets(valuesFor(myColoradoDopplerKeys), coMyColorado);
    assert.deepEqual(routed.api.map((secret) => secret.appKey), myColoradoAppKeys);
  });

  for (const [mode, config] of [
    ["CO with Keycloak", coKeycloak],
    ["DC", configFor("dc")],
    ["DC with a stray CO_SIGN_IN", configFor("dc", "mycolorado")],
  ] as const) {
    it(`holds back the MyColorado client keys for ${mode}, and does not report them as unlisted`, () => {
      const routed = routeSecrets(valuesFor(myColoradoDopplerKeys), config);
      assert.deepEqual(routed.api, []);
      assert.deepEqual(routed.notForwarded, []);
    });
  }
});

describe("assertSignInSecrets", () => {
  it("passes when MyColorado sign-in has each client key", () => {
    const routed = routeSecrets(valuesFor(myColoradoDopplerKeys), coMyColorado);
    assert.doesNotThrow(() => assertSignInSecrets(fromDoppler, routed, coMyColorado));
  });

  it("stops MyColorado sign-in and names each missing Doppler key", () => {
    const routed = routeSecrets(valuesFor(["OIDC__CLIENTID"]), coMyColorado);
    assert.throws(
      () => assertSignInSecrets(fromDoppler, routed, coMyColorado),
      (error: Error) =>
        ["OIDC__CLIENTSECRET", "OIDC__STEPUP__CLIENTID", "OIDC__STEPUP__CLIENTSECRET"].every(
          (key) => error.message.includes(key),
        ),
    );
  });

  it("tells MyColorado sign-in to set up Doppler when the repo has no Doppler setup", () => {
    assert.throws(
      () => assertSignInSecrets(dopplerNotSetUp, noneRouted, coMyColorado),
      (error: Error) =>
        error.message.includes("doppler setup --no-interactive") &&
        !error.message.includes("OIDC__CLIENTID"),
    );
  });

  it("tells MyColorado sign-in that USE_DOPPLER turned Doppler off", () => {
    assert.throws(
      () => assertSignInSecrets(dopplerDisabled, noneRouted, coMyColorado),
      (error: Error) =>
        error.message.includes("USE_DOPPLER") && !error.message.includes("doppler setup"),
    );
  });

  for (const secrets of [fromDoppler, dopplerNotSetUp, dopplerDisabled]) {
    const label = secrets.source === "doppler" ? "Doppler" : secrets.reason;
    it(`asks nothing of Keycloak sign-in when the secret source is ${label}`, () => {
      assert.doesNotThrow(() => assertSignInSecrets(secrets, noneRouted, coKeycloak));
    });
  }
});

describe("assertNoCapabilityOverlap", () => {
  it("stops when a forwarded key matches a key a capability sets, whatever the casing", () => {
    const routed = routeSecrets(valuesFor(["JWTSETTINGS__SECRETKEY"]), coKeycloak);
    assert.throws(
      () => assertNoCapabilityOverlap(routed, ["jwtsettings__secretkey"]),
      /JwtSettings__SecretKey/,
    );
  });

  it("allows forwarded keys that no capability sets", () => {
    const routed = routeSecrets(valuesFor(allDopplerKeys), coMyColorado);
    assert.doesNotThrow(() =>
      assertNoCapabilityOverlap(routed, ["Cbms__UseMockResponses", "Socure__Enabled"]),
    );
  });
});
