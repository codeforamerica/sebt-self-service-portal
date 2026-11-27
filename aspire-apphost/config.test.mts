import { strict as assert } from "node:assert";
import { describe, it } from "node:test";

import { parseCoSignIn, parseUseDoppler } from "./config.mjs";

describe("parseUseDoppler", () => {
  it("uses Doppler when USE_DOPPLER is unset or empty", () => {
    assert.equal(parseUseDoppler(undefined), true);
    assert.equal(parseUseDoppler(""), true);
  });

  for (const value of ["false", "FALSE", "0", " false "]) {
    it(`turns Doppler off for '${value}'`, () => {
      assert.equal(parseUseDoppler(value), false);
    });
  }

  for (const value of ["true", "maybe"]) {
    it(`rejects '${value}' and names the accepted value`, () => {
      assert.throws(
        () => parseUseDoppler(value),
        (error: Error) =>
          /USE_DOPPLER/.test(error.message) &&
          error.message.includes(`'${value}'`) &&
          /false/.test(error.message),
      );
    });
  }
});

describe("parseCoSignIn", () => {
  it("returns keycloak when CO_SIGN_IN is unset or empty", () => {
    assert.equal(parseCoSignIn(undefined), "keycloak");
    assert.equal(parseCoSignIn(""), "keycloak");
  });

  for (const value of ["mycolorado", "MyColorado", "MYCOLORADO", " mycolorado "]) {
    it(`returns mycolorado for '${value}'`, () => {
      assert.equal(parseCoSignIn(value), "mycolorado");
    });
  }

  for (const value of ["keycloak", "KEYCLOAK", " keycloak "]) {
    it(`returns keycloak for '${value}'`, () => {
      assert.equal(parseCoSignIn(value), "keycloak");
    });
  }

  for (const value of ["other", "maybe", "myco", "pingone"]) {
    it(`rejects '${value}' and names the accepted values`, () => {
      assert.throws(
        () => parseCoSignIn(value),
        (error: Error) =>
          /CO_SIGN_IN/.test(error.message) &&
          error.message.includes(`'${value}'`) &&
          /keycloak/.test(error.message) &&
          /mycolorado/.test(error.message),
      );
    });
  }
});
