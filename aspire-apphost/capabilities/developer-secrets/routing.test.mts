import { strict as assert } from "node:assert";
import { describe, it } from "node:test";

import {
  assertNoCapabilityOverlap,
  forwardedSecrets,
  routeSecrets,
} from "./routing.mjs";

const allDopplerKeys = forwardedSecrets.map((secret) => secret.dopplerKey);

function valuesFor(keys: readonly string[]): Map<string, string> {
  return new Map(keys.map((key) => [key, `value-of-${key}`]));
}

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
    const routed = routeSecrets(valuesFor([...allDopplerKeys, "EXTRA_KEY", "ANOTHER"]));
    for (const target of ["api", "web"] as const) {
      assert.deepEqual(
        routed[target].map((secret) => secret.appKey),
        forwardedSecrets.filter((secret) => secret.target === target).map((secret) => secret.appKey),
      );
    }
    assert.deepEqual(routed.notForwarded, ["ANOTHER", "EXTRA_KEY"]);
  });

  it("forwards a key under the casing the app uses", () => {
    const routed = routeSecrets(valuesFor(["JWTSETTINGS__SECRETKEY"]));
    assert.deepEqual(
      routed.api.map((secret) => [secret.appKey, secret.value]),
      [["JwtSettings__SecretKey", "value-of-JWTSETTINGS__SECRETKEY"]],
    );
  });

  it("gives each secret a valid Aspire parameter name", () => {
    const routed = routeSecrets(valuesFor(allDopplerKeys));
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
    const routed = routeSecrets(valuesFor(["SMARTY__AUTHID"]));
    assert.deepEqual(routed.api.map((secret) => secret.appKey), ["Smarty__AuthId"]);
    assert.deepEqual(routed.web, []);
    assert.deepEqual(routed.notForwarded, []);
  });

  it("skips a listed key whose value is empty, so it cannot blank out a local value", () => {
    const routed = routeSecrets(new Map([["SMARTY__AUTHID", ""]]));
    assert.deepEqual(routed.api, []);
  });
});

describe("assertNoCapabilityOverlap", () => {
  it("stops when a forwarded key matches a key a capability sets, whatever the casing", () => {
    const routed = routeSecrets(valuesFor(["JWTSETTINGS__SECRETKEY"]));
    assert.throws(
      () => assertNoCapabilityOverlap(routed, ["jwtsettings__secretkey"]),
      /JwtSettings__SecretKey/,
    );
  });

  it("allows forwarded keys that no capability sets", () => {
    const routed = routeSecrets(valuesFor(allDopplerKeys));
    assert.doesNotThrow(() =>
      assertNoCapabilityOverlap(routed, ["Cbms__UseMockResponses", "Socure__Enabled"]),
    );
  });
});
